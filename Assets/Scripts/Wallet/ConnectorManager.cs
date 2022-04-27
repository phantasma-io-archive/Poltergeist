using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using LunarLabs.WebSockets;
using LunarLabs.Parser.JSON;
using System;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Text;
using LunarLabs.WebServer.HTTP;
using System.Security.Cryptography;
using Phantasma.Domain;
using Phantasma.SDK;


namespace Poltergeist
{

    public class ConnectorManager : MonoBehaviour
    {
        public WalletConnector PhantasmaLink { get; private set; }
        public static ConnectorManager Instance { get; private set; }

        private Socket listener;

        private Dictionary<string, Action<WebSocket>> _websocketsHandlers = new Dictionary<string, Action<WebSocket>>();
        private List<WebSocket> _activeWebsockets = new List<WebSocket>();
        private Func<MemoryStream> _bufferFactory;
        private BufferPool _bufferPool;

        public DateTime StartTime { get; private set; }

        // Start is called before the first frame update
        void Start()
        {
            Instance = this;

            this.PhantasmaLink = new WalletConnector();

            var port = WalletLink.WebSocketPort;

            Log.Write("Starting websocket server for Phantasma wallet connector at port " + port);

            this.StartTime = DateTime.Now;

            _bufferPool = new BufferPool();
            _bufferFactory = _bufferPool.GetBuffer;

            // Create a TCP/IP socket
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Blocking = true;
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);


            var localEndPoint = new IPEndPoint(IPAddress.Any, port);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(20);

            }
            catch (Exception e)
            {
                Log.WriteError(e.ToString());
                return;
            }

            this.WebSocket("/phantasma", (socket) =>
            {
                while (socket.IsOpen)
                {
                    var msg = socket.Receive();

                    if (msg.CloseStatus == WebSocketCloseStatus.None)
                    {
                        var str = Encoding.UTF8.GetString(msg.Bytes);

                        WalletGUI.Instance.CallOnUIThread(() =>
                        {
                            PhantasmaLink.Execute(str, (id, root, success) =>
                            {
                                root.AddField("id", id);
                                root.AddField("success", success);

                                var json = JSONWriter.WriteToString(root);

                                try
                                {
                                    socket.Send(json);
                                    IntentPluginManager.Instance.ReturnMessage(json);
                                }
                                catch (Exception e)
                                {
                                    Log.WriteWarning("websocket send failure, while answering phantasma link request: " + str + "\nExcepion: " + e.Message);
                                }
                            });
                        });
                    }
                }
            });

            try
            {
                var client = listener.BeginAccept(new System.AsyncCallback(OnClientConnect), listener);
            }
            catch (Exception e)
            {
                Log.WriteError(e.ToString());
            }
        }

        private void OnDestroy()
        {
            Log.Write("Stopping websocket server");

            if (listener != null)
            {
                listener.BeginDisconnect(false, new System.AsyncCallback(OnEndHostComplete), listener);
            }
        }

        void OnEndHostComplete(System.IAsyncResult result)
        {
            listener = null;
        }

        // Update is called once per frame
        void Update()
        {
            lock (_activeWebsockets)
            {
                foreach (var socket in _activeWebsockets)
                {
                    if (socket.State == WebSocketState.Open && socket.NeedsPing)
                    {
                        var diff = DateTime.UtcNow - socket.LastPingPong;
                        if (diff.TotalMilliseconds >= socket.KeepAliveInterval)
                        {
                            socket.SendPing();
                        }
                    }
                }
            }
        }

        private void OnClientConnect(System.IAsyncResult result)
        {
            Log.Write("Incoming client connecting");

            Socket client;
            try
            {
                client = listener.EndAccept(result);
            }
            catch (System.Exception e)
            {
                Log.WriteError("Exception when accepting incoming connection: " + e);
                return;
            }

            try
            {
                listener.BeginAccept(new System.AsyncCallback(OnClientConnect), listener);
            }
            catch (System.Exception e)
            {
                Log.WriteError("Exception when starting new accept process: " + e);
            }

            try
            {
                // Disable the Nagle Algorithm for this tcp socket.
                client.NoDelay = true;

                // Set the receive buffer size to 8k
                client.ReceiveBufferSize = 8192;

                // Set the timeout for synchronous receive methods
                //client.ReceiveTimeout = 5000;

                // Set the send buffer size to 8k.
                client.SendBufferSize = 8192;

                // Set the timeout for synchronous send methods
                //client.SendTimeout = 5000;

                // Set the Time To Live (TTL) to 42 router hops.
                client.Ttl = 42;

                bool keepAlive = false;

                int requestCount = 0;

                using (var stream = new NetworkStream(client))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        using (var writer = new BinaryWriter(stream))
                        {
                            {
                                do
                                {
                                    var lines = new List<string>();
                                    HTTPRequest request = null;

                                    var line = new StringBuilder();
                                    char prevChar;
                                    char currentChar = '\0';
                                    while (true)
                                    {
                                        prevChar = currentChar;
                                        currentChar = (char)reader.ReadByte();

                                        if (currentChar == '\n' && prevChar == '\r')
                                        {
                                            if (line.Length == 0)
                                            {
                                                request = new HTTPRequest();
                                                break;
                                            }

                                            var temp = line.ToString();
                                            Log.Write(temp, Log.Level.Debug1);

                                            if (temp.Contains("\0"))
                                            {
                                                throw new Exception("Null Byte Injection detected");
                                            }

                                            lines.Add(temp);
                                            line.Length = 0;
                                        }
                                        else
                                        if (currentChar != '\r' && currentChar != '\n')
                                        {
                                            line.Append(currentChar);
                                        }
                                    }

                                    bool isWebSocket = false;

                                    // parse headers
                                    if (request != null)
                                    {
                                        for (int i = 1; i < lines.Count; i++)
                                        {
                                            var temp = lines[i].Split(':');
                                            if (temp.Length >= 2)
                                            {
                                                var key = temp[0];
                                                var val = temp[1].TrimStart();

                                                request.headers[key] = val;

                                                if (key.Equals("Content-Length", StringComparison.InvariantCultureIgnoreCase))
                                                {
                                                    int contentLength = int.Parse(val);
                                                    request.bytes = reader.ReadBytes(contentLength);
                                                }
                                                else
                                                if (key.Equals("Upgrade", StringComparison.InvariantCultureIgnoreCase))
                                                {
                                                    isWebSocket = true;
                                                }
                                            }
                                        }
                                    }

                                    if (request != null)
                                    {
                                        var s = lines[0].Split(' ');

                                        if (s.Length == 3)
                                        {
                                            switch (s[0].ToUpperInvariant())
                                            {
                                                case "GET": request.method = HTTPRequest.Method.Get; break;
                                                case "POST": request.method = HTTPRequest.Method.Post; break;
                                                case "HEAD": request.method = HTTPRequest.Method.Head; break;
                                                case "PUT": request.method = HTTPRequest.Method.Put; break;
                                                case "DELETE": request.method = HTTPRequest.Method.Delete; break;

                                                default: throw new Exception("Invalid HTTP method: " + s[0]);
                                            }

                                            request.version = s[2];

                                            var path = s[1].Split('?');
                                            request.path = path[0];
                                            request.url = s[1];

                                            Log.Write(request.method.ToString() + " " + s[1], Log.Level.Debug1);

                                            if (isWebSocket)
                                            {
                                                Action<WebSocket> handler = null;
                                                string targetProtocol = null;

                                                var protocolHeader = "Sec-WebSocket-Protocol";

                                                if (request.headers.ContainsKey(protocolHeader))
                                                {
                                                    var protocols = request.headers[protocolHeader].Split(',').Select(x => x.Trim());
                                                    foreach (var protocol in protocols)
                                                    {
                                                        var key = MakeWebSocketKeyPair(protocol, request.path);
                                                        if (_websocketsHandlers.ContainsKey(key))
                                                        {
                                                            targetProtocol = protocol;
                                                            handler = _websocketsHandlers[key];
                                                            break;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    targetProtocol = null;
                                                    var key = MakeWebSocketKeyPair(targetProtocol, request.path);
                                                    if (_websocketsHandlers.ContainsKey(key))
                                                    {
                                                        handler = _websocketsHandlers[key];
                                                    }
                                                }

                                                if (handler != null)
                                                {
                                                    var key = request.headers["Sec-WebSocket-Key"];
                                                    key = GenerateWebSocketKey(key);

                                                    var sb = new StringBuilder();
                                                    sb.Append("HTTP/1.1 101 Switching Protocols\r\n");
                                                    sb.Append("Upgrade: websocket\r\n");
                                                    sb.Append("Connection: Upgrade\r\n");
                                                    sb.Append($"Sec-WebSocket-Accept: {key}\r\n");
                                                    if (targetProtocol != null)
                                                    {
                                                        sb.Append($"Sec-WebSocket-Protocol: {targetProtocol}\r\n");
                                                    }
                                                    sb.Append("\r\n");

                                                    var bytes = Encoding.ASCII.GetBytes(sb.ToString());
                                                    writer.Write(bytes);

                                                    string secWebSocketExtensions = null;
                                                    var keepAliveInterval = 5000;
                                                    var includeExceptionInCloseResponse = true;
                                                    var webSocket = new WebSocket(_bufferFactory, stream, keepAliveInterval, secWebSocketExtensions, includeExceptionInCloseResponse, false, targetProtocol);
                                                    lock (_activeWebsockets)
                                                    {
                                                        _activeWebsockets.Add(webSocket);
                                                    }
                                                    handler(webSocket);
                                                    lock (_activeWebsockets)
                                                    {
                                                        _activeWebsockets.Remove(webSocket);
                                                    }
                                                }
                                                else
                                                {

                                                }

                                                return;
                                            }
                                            else
                                            {
                                                Log.WriteError("Not a valid websocket request");
                                            }
                                        }
                                        else
                                        {
                                            Log.WriteError("Failed parsing request method");
                                        }

                                    }
                                    else
                                    {
                                        Log.WriteError("Failed parsing request data");
                                    }

                                    requestCount++;
                                } while (keepAlive);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Write(e.ToString());
            }
            finally
            {
                client.Close();
            }
        }
        private string MakeWebSocketKeyPair(string protocol, string path)
        {
            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }

            if (protocol == null)
            {
                return path;
            }

            return $"{protocol}:{path}";
        }

        public static string GenerateWebSocketKey(string input)
        {
            var output = input + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

            using (var sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(output));
                return System.Convert.ToBase64String(hash);
            }
        }

        public void WebSocket(string path, Action<WebSocket> handler, params string[] protocols)
        {
            if (protocols == null || protocols.Length == 0)
            {
                var key = MakeWebSocketKeyPair(null, path);
                _websocketsHandlers[key] = handler;
            }
            else
            {
                foreach (var protocol in protocols)
                {
                    var key = MakeWebSocketKeyPair(protocol, path);
                    _websocketsHandlers[key] = handler;
                }
            }
        }
        
        #region Intents
        public void OnIntentInteraction(string msg)
        {
#if UNITY_ANDROID
            var str = msg;

            WalletGUI.Instance.CallOnUIThread(() =>
            {
                PhantasmaLink.Execute(str, (id, root, success) =>
                {
                    root.AddField("id", id);
                    root.AddField("success", success);

                    var json = JSONWriter.WriteToString(root);

                    try
                    {
                        IntentPluginManager.Instance.ReturnMessage(json);
                    }
                    catch (Exception e)
                    {
                        Log.WriteWarning("websocket send failure, while answering phantasma link request: " + str + "\nExcepion: " + e.Message);
                    }
                });
            });
#endif
        }
        #endregion
    }
}
