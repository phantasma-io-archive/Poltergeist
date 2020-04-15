using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace LunarLabs.WebSockets
{
    internal enum WebSocketOpCode
    {
        ContinuationFrame = 0,
        TextFrame = 1,
        BinaryFrame = 2,
        ConnectionClose = 8,
        Ping = 9,
        Pong = 10
    }

    //     Defines the different states a WebSockets instance can be in.
    public enum WebSocketState
    {
        None = 0,        //     Reserved for future use.
        Connecting = 1,        //     The connection is negotiating the handshake with the remote endpoint.
        Open = 2,        //     The initial state after the HTTP handshake has been completed.
        CloseSent = 3,        //     A close message was sent to the remote endpoint.    
        CloseReceived = 4,        //     A close message was received from the remote endpoint.    
        Closed = 5,        //     Indicates the WebSocket close handshake completed gracefully.
        Aborted = 6        //     Reserved for future use.
    }

    //     Indicates the message type.
    public enum WebSocketMessageType
    {
        Text = 0,        //     The message is clear text.
        Binary = 1,        //     The message is in binary format.
        Close = 2        //     A receive has completed because a close message was received.
    }

    //     Represents well known WebSocket close codes as defined in http://www.rfc-editor.org/rfc/rfc6455.txt
    public enum WebSocketCloseStatus
    {
        None = 0,
        //     (1000) The connection has closed after the request was fulfilled.
        NormalClosure = 1000,
        //     (1001) Indicates an endpoint is being removed. Either the server or client will become unavailable.
        EndpointUnavailable = 1001,
        //     (1002) The client or server is terminating the connection because of a protocol error.
        ProtocolError = 1002,
        //     (1003) The client or server is terminating the connection because it cannot accept the data type it received.
        InvalidMessageType = 1003,
        //     No error specified.
        Empty = 1005,
        //     (1007) The client or server is terminating the connection because it has received data inconsistent with the message type.
        InvalidPayloadData = 1007,
        //     (1008) The connection will be closed because an endpoint has received a message that violates its policy.
        PolicyViolation = 1008,
        //     (1004) Reserved for future use.
        MessageTooBig = 1009,
        //     (1010) The client is terminating the connection because it expected the server to negotiate an extension.
        MandatoryExtension = 1010,
        //     The connection will be closed by the server because of an error on the server.
        InternalServerError = 1011
    }

    public struct WebSocketReceiveResult
    {
        public WebSocketReceiveResult(int count, WebSocketMessageType messageType, bool endOfMessage, ArraySegment<byte> bytes)
        {
            this.Count = count;
            this.MessageType = messageType;
            this.EndOfMessage = endOfMessage;
            this.Bytes = new byte[count];
            this.CloseStatus = WebSocketCloseStatus.None;
            this.CloseStatusDescription = null;
            Buffer.BlockCopy(bytes.Array, bytes.Offset, this.Bytes, 0, count);
        }

        public WebSocketReceiveResult(int count, WebSocketMessageType messageType, bool endOfMessage, WebSocketCloseStatus closeStatus, string closeStatusDescription)
        {
            this.Count = count;
            this.MessageType = messageType;
            this.EndOfMessage = endOfMessage;
            this.Bytes = null;
            this.CloseStatus = closeStatus;
            this.CloseStatusDescription = closeStatusDescription;
        }

        //     Indicates the reason why the remote endpoint initiated the close handshake.
        public WebSocketCloseStatus CloseStatus { get; }

        //     Returns the optional description that describes why the close handshake has been initiated by the remote endpoint.
        public string CloseStatusDescription { get; }

        //     Indicates the number of bytes that the WebSocket received.
        public int Count { get; }

        public byte[] Bytes { get; }

        //     Indicates whether the message has been received completely.
        public bool EndOfMessage { get; }

        //     Indicates whether the current message is a UTF-8 message or a binary message.
        public WebSocketMessageType MessageType { get; }
    }

    public class WebSocket
    {
        private readonly Func<MemoryStream> _recycledStreamFactory;
        private readonly Stream _stream;
        private readonly bool _includeExceptionInCloseResponse;
        private readonly bool _isClient;
        private readonly string _subProtocol;
        private WebSocketState _state;
        private bool _isContinuationFrame;
        private WebSocketMessageType _continuationFrameMessageType = WebSocketMessageType.Binary;
        private readonly bool _usePerMessageDeflate = false;
        const int MAX_PING_PONG_PAYLOAD_LEN = 125;
        private WebSocketCloseStatus? _closeStatus;
        private string _closeStatusDescription;

        private ArraySegment<byte> _receiveBuffer = new ArraySegment<byte>(new byte[1024 * 8]);

        public WebSocket(Func<MemoryStream> recycledStreamFactory, Stream stream, int keepAliveInterval, string secWebSocketExtensions, bool includeExceptionInCloseResponse, bool isClient, string subProtocol)
        {
            _recycledStreamFactory = recycledStreamFactory;
            _stream = stream;
            _isClient = isClient;
            _subProtocol = subProtocol;
            _state = WebSocketState.Open;

            if (secWebSocketExtensions?.IndexOf("permessage-deflate") >= 0)
            {
                _usePerMessageDeflate = true;
            }
            Log.Write("using websocket compression: "+ _usePerMessageDeflate);

            KeepAliveInterval = keepAliveInterval;
            _includeExceptionInCloseResponse = includeExceptionInCloseResponse;
            if (keepAliveInterval <= 0)
            {
                throw new InvalidOperationException("KeepAliveInterval must be positive");
            }

            LastPingPong = DateTime.UtcNow;
            NeedsPing = true;
            _pingCounter = (int)LastPingPong.Ticks;
        }

        public WebSocketCloseStatus? CloseStatus => _closeStatus;

        public string CloseStatusDescription => _closeStatusDescription;

        public WebSocketState State { get { return _state; } }

        public string SubProtocol => _subProtocol;

        public int KeepAliveInterval { get; private set; }

        public DateTime LastPingPong { get; private set; }
        public bool NeedsPing { get; private set; }
        public bool IsOpen => State == WebSocketState.Open;

        private int _pingCounter;
        private byte[] _pingPayload;

        public WebSocketReceiveResult Receive()
        {
            try
            {
                // we may receive control frames so reading needs to happen in an infinite loop
                while (true)
                {
                    // allow this operation to be cancelled from iniside OR outside this instance
                    WebSocketFrame frame = null;
                    try
                    {
                        frame = WebSocketFrameReader.Read(_stream, _receiveBuffer);
                        Log.Write("websocket.ReceivedFrame: " + frame.OpCode + ", " + frame.IsFinBitSet + ", " + frame.Count, (frame.OpCode.ToString() == "Pong") ? Log.Level.Debug1 : Log.Level.Logic);
                    }
                    catch (InternalBufferOverflowException ex)
                    {
                        CloseOutputAutoTimeout(WebSocketCloseStatus.MessageTooBig, "Frame too large to fit in buffer. Use message fragmentation", ex);
                        throw;
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        CloseOutputAutoTimeout(WebSocketCloseStatus.ProtocolError, "Payload length out of range", ex);
                        throw;
                    }
                    catch (EndOfStreamException ex)
                    {
                        CloseOutputAutoTimeout(WebSocketCloseStatus.InvalidPayloadData, "Unexpected end of stream encountered", ex);
                        throw;
                    }
                    catch (OperationCanceledException ex)
                    {
                        CloseOutputAutoTimeout(WebSocketCloseStatus.EndpointUnavailable, "Operation cancelled", ex);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        CloseOutputAutoTimeout(WebSocketCloseStatus.InternalServerError, "Error reading WebSocket frame", ex);
                        throw;
                    }

                    switch (frame.OpCode)
                    {
                        case WebSocketOpCode.ConnectionClose:
                            return RespondToCloseFrame(frame, _receiveBuffer);

                        case WebSocketOpCode.Ping:
                            ArraySegment<byte> pingPayload = new ArraySegment<byte>(_receiveBuffer.Array, _receiveBuffer.Offset, frame.Count);
                            SendPong(pingPayload);
                            break;

                        case WebSocketOpCode.Pong:
                            ArraySegment<byte> pongBuffer = new ArraySegment<byte>(_receiveBuffer.Array, frame.Count, _receiveBuffer.Offset);
                            if (pongBuffer.Array.SequenceEqual(_pingPayload))
                            {

                            }
                            LastPingPong = DateTime.UtcNow;
                            NeedsPing = true;
                            break;

                        case WebSocketOpCode.TextFrame:
                            if (!frame.IsFinBitSet)
                            {
                                // continuation frames will follow, record the message type Text
                                _continuationFrameMessageType = WebSocketMessageType.Text;
                            }
                            return new WebSocketReceiveResult(frame.Count, WebSocketMessageType.Text, frame.IsFinBitSet, _receiveBuffer);

                        case WebSocketOpCode.BinaryFrame:
                            if (!frame.IsFinBitSet)
                            {
                                // continuation frames will follow, record the message type Binary
                                _continuationFrameMessageType = WebSocketMessageType.Binary;
                            }
                            return new WebSocketReceiveResult(frame.Count, WebSocketMessageType.Binary, frame.IsFinBitSet, _receiveBuffer);

                        case WebSocketOpCode.ContinuationFrame:
                            return new WebSocketReceiveResult(frame.Count, _continuationFrameMessageType, frame.IsFinBitSet, _receiveBuffer);

                        default:
                            Exception ex = new NotSupportedException($"Unknown WebSocket opcode {frame.OpCode}");
                            CloseOutputAutoTimeout(WebSocketCloseStatus.ProtocolError, ex.Message, ex);
                            throw ex;
                    }
                }
            }
            catch (Exception catchAll)
            {
                // Most exceptions will be caught closer to their source to send an appropriate close message (and set the WebSocketState)
                // However, if an unhandled exception is encountered and a close message not sent then send one here
                if (_state == WebSocketState.Open)
                {
                    CloseOutputAutoTimeout(WebSocketCloseStatus.InternalServerError, "Unexpected error reading from WebSocket", catchAll);
                }

                throw;
            }
        }

        public void Send(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            Send(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true);
        }

        public void Send(byte[] bytes)
        {
            Send(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true);
        }

        /// Send data to the web socket
        public void Send(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage)
        {
            using (MemoryStream stream = _recycledStreamFactory())
            {
                WebSocketOpCode opCode = GetOppCode(messageType);

                if (_usePerMessageDeflate)
                {
                    // NOTE: Compression is currently work in progress and should NOT be used in this library.
                    // The code below is very inefficient for small messages. Ideally we would like to have some sort of moving window
                    // of data to get the best compression. And we don't want to create new buffers which is bad for GC.
                    using (MemoryStream temp = new MemoryStream())
                    {
                        DeflateStream deflateStream = new DeflateStream(temp, CompressionMode.Compress);
                        deflateStream.Write(buffer.Array, buffer.Offset, buffer.Count);
                        deflateStream.Flush();
                        var compressedBuffer = new ArraySegment<byte>(temp.ToArray());
                        WebSocketFrameWriter.Write(opCode, compressedBuffer, stream, endOfMessage, _isClient);
                        Log.Write($"websocket.SendingFrame: {opCode}, {endOfMessage}, {compressedBuffer.Count}, compressed");
                    }
                }
                else
                {
                    WebSocketFrameWriter.Write(opCode, buffer, stream, endOfMessage, _isClient);
                    Log.Write($"websocket.SendingFrame: {opCode}, {endOfMessage}, {buffer.Count}, uncompressed");
                }

                WriteStreamToNetwork(stream);
                _isContinuationFrame = !endOfMessage; // TODO: is this correct??
            }
        }

        /// Call this automatically from server side each keepAliveInterval period
        /// NOTE: ping payload must be 125 bytes or less
        public void SendPing()
        {
            _pingCounter++;
            _pingPayload = BitConverter.GetBytes(_pingCounter);
            
            var payload = new ArraySegment<byte>(_pingPayload);
            if (payload.Count > MAX_PING_PONG_PAYLOAD_LEN)
            {
                throw new InvalidOperationException($"Cannot send Ping: Max ping message size {MAX_PING_PONG_PAYLOAD_LEN} exceeded: {payload.Count}");
            }

            if (_state == WebSocketState.Open)
            {
                using (MemoryStream stream = _recycledStreamFactory())
                {
                    NeedsPing = false;
                    WebSocketFrameWriter.Write(WebSocketOpCode.Ping, payload, stream, true, _isClient);
                    Log.Write($"websocket.Ping: {payload.Count}", Log.Level.Debug1);
                    WriteStreamToNetwork(stream);
                }
            }
        }

        /// Polite close (use the close handshake)
        public void Close(WebSocketCloseStatus closeStatus, string statusDescription)
        {
            if (_state == WebSocketState.Open)
            {
                using (MemoryStream stream = _recycledStreamFactory())
                {
                    ArraySegment<byte> buffer = BuildClosePayload(closeStatus, statusDescription);
                    WebSocketFrameWriter.Write(WebSocketOpCode.ConnectionClose, buffer, stream, true, _isClient);
                    Log.Write($"websocket.Close: {closeStatus}, {statusDescription}");
                    WriteStreamToNetwork(stream);
                    _state = WebSocketState.CloseSent;
                }
            }
            else
            {
                Log.Write($"websocket already closed");
            }
        }

        /// Fire and forget close
        public void CloseOutput(WebSocketCloseStatus closeStatus, string statusDescription)
        {
            if (_state == WebSocketState.Open)
            {
                _state = WebSocketState.Closed; // set this before we write to the network because the write may fail

                using (MemoryStream stream = _recycledStreamFactory())
                {
                    ArraySegment<byte> buffer = BuildClosePayload(closeStatus, statusDescription);
                    WebSocketFrameWriter.Write(WebSocketOpCode.ConnectionClose, buffer, stream, true, _isClient);
                    WriteStreamToNetwork(stream);
                }
            }
        }

        /// Dispose will send a close frame if the connection is still open
        public void Dispose()
        {
            if (_state == WebSocketState.Open)
            {
                CloseOutput(WebSocketCloseStatus.EndpointUnavailable, "Service is Disposed");
            }

            _stream.Close();
        }

        /// As per the spec, write the close status followed by the close reason
        private ArraySegment<byte> BuildClosePayload(WebSocketCloseStatus closeStatus, string statusDescription)
        {
            byte[] statusBuffer = BitConverter.GetBytes((ushort)closeStatus);
            Array.Reverse(statusBuffer); // network byte order (big endian)

            if (statusDescription == null)
            {
                return new ArraySegment<byte>(statusBuffer);
            }
            else
            {
                byte[] descBuffer = Encoding.UTF8.GetBytes(statusDescription);
                byte[] payload = new byte[statusBuffer.Length + descBuffer.Length];
                Buffer.BlockCopy(statusBuffer, 0, payload, 0, statusBuffer.Length);
                Buffer.BlockCopy(descBuffer, 0, payload, statusBuffer.Length, descBuffer.Length);
                return new ArraySegment<byte>(payload);
            }
        }

        /// NOTE: pong payload must be 125 bytes or less
        /// Pong should contain the same payload as the ping
        private void SendPong(ArraySegment<byte> payload)
        {
            // as per websocket spec
            if (payload.Count > MAX_PING_PONG_PAYLOAD_LEN)
            {
                Exception ex = new InvalidOperationException($"Max ping message size {MAX_PING_PONG_PAYLOAD_LEN} exceeded: {payload.Count}");
                CloseOutputAutoTimeout(WebSocketCloseStatus.ProtocolError, ex.Message, ex);
                throw ex;
            }

            try
            {
                if (_state == WebSocketState.Open)
                {
                    using (MemoryStream stream = _recycledStreamFactory())
                    {
                        WebSocketFrameWriter.Write(WebSocketOpCode.Pong, payload, stream, true, _isClient);
                        Log.Write($"websocket.Pong: {payload.Count}");
                        WriteStreamToNetwork(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                CloseOutputAutoTimeout(WebSocketCloseStatus.EndpointUnavailable, "Unable to send Pong response", ex);
                throw;
            }
        }

        /// Called when a Close frame is received, sends a response close frame if applicable
        private WebSocketReceiveResult RespondToCloseFrame(WebSocketFrame frame, ArraySegment<byte> buffer)
        {
            _closeStatus = frame.CloseStatus;
            _closeStatusDescription = frame.CloseStatusDescription;

            if (_state == WebSocketState.CloseSent)
            {
                // this is a response to close handshake initiated by this instance
                _state = WebSocketState.Closed;
            }
            else if (_state == WebSocketState.Open)
            {
                // do not echo the close payload back to the client, there is no requirement for it in the spec. 
                // However, the same CloseStatus as recieved should be sent back.
                ArraySegment<byte> closePayload = new ArraySegment<byte>(new byte[0], 0, 0);
                _state = WebSocketState.CloseReceived;

                using (MemoryStream stream = _recycledStreamFactory())
                {
                    WebSocketFrameWriter.Write(WebSocketOpCode.ConnectionClose, closePayload, stream, true, _isClient);
                    WriteStreamToNetwork(stream);
                }
            }

            return new WebSocketReceiveResult(frame.Count, WebSocketMessageType.Close, frame.IsFinBitSet, frame.CloseStatus, frame.CloseStatusDescription);
        }

        /// Note that the way in which the stream buffer is accessed can lead to significant performance problems
        /// You want to avoid a call to stream.ToArray to avoid extra memory allocation
        /// MemoryStream can be configured to have its internal buffer accessible. 
        private ArraySegment<byte> GetBuffer(MemoryStream stream)
        {
            // Avoid calling ToArray on the MemoryStream because it allocates a new byte array on tha heap
            // We avaoid this by attempting to access the internal memory stream buffer
            // This works with supported streams like the recyclable memory stream and writable memory streams
            if (!stream.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                // internal buffer not suppoted, fall back to ToArray()
                byte[] array = stream.ToArray();
                buffer = new ArraySegment<byte>(array, 0, array.Length);
            }

            return new ArraySegment<byte>(buffer.Array, buffer.Offset, (int)stream.Position);
        }

        /// Puts data on the wire
        private void WriteStreamToNetwork(MemoryStream stream)
        {
            ArraySegment<byte> buffer = GetBuffer(stream);
            _stream.Write(buffer.Array, buffer.Offset, buffer.Count);
        }

        /// Turns a spec websocket frame opcode into a WebSocketMessageType
        private WebSocketOpCode GetOppCode(WebSocketMessageType messageType)
        {
            if (_isContinuationFrame)
            {
                return WebSocketOpCode.ContinuationFrame;
            }
            else
            {
                switch (messageType)
                {
                    case WebSocketMessageType.Binary:
                        return WebSocketOpCode.BinaryFrame;

                    case WebSocketMessageType.Text:
                        return WebSocketOpCode.TextFrame;

                    case WebSocketMessageType.Close:
                        throw new NotSupportedException("Cannot use Send function to send a close frame. Use Close function.");

                    default:
                        throw new NotSupportedException($"MessageType {messageType} not supported");
                }
            }
        }

        /// Automatic WebSocket close in response to some invalid data from the remote websocket host
        private void CloseOutputAutoTimeout(WebSocketCloseStatus closeStatus, string statusDescription, Exception ex)
        {
            // we may not want to send sensitive information to the client / server
            if (_includeExceptionInCloseResponse)
            {
                statusDescription = statusDescription + "\r\n\r\n" + ex.ToString();
            }

            CloseOutput(closeStatus, statusDescription);
        }
    }
}
