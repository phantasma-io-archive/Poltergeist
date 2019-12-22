using System;

namespace LunarLabs.WebSockets
{
    internal class WebSocketFrame
    {
        public bool IsFinBitSet { get; private set; }

        public WebSocketOpCode OpCode { get; private set; }

        public int Count { get; private set; }

        public WebSocketCloseStatus CloseStatus { get; private set; }

        public string CloseStatusDescription { get; private set; }

        public WebSocketFrame(bool isFinBitSet, WebSocketOpCode webSocketOpCode, int count)
        {
            IsFinBitSet = isFinBitSet;
            OpCode = webSocketOpCode;
            Count = count;
            CloseStatus = WebSocketCloseStatus.None;
            CloseStatusDescription = null;
        }

        public WebSocketFrame(bool isFinBitSet, WebSocketOpCode webSocketOpCode, int count, WebSocketCloseStatus closeStatus, string closeStatusDescription) : this(isFinBitSet, webSocketOpCode, count)
        {
            CloseStatus = closeStatus;
            CloseStatusDescription = closeStatusDescription;
        }
    }

    internal static class WebSocketFrameExtensions
    {
        public const int MaskKeyLength = 4;

        /// <summary>
        /// Mutate payload with the mask key
        /// This is a reversible process
        /// If you apply this to masked data it will be unmasked and visa versa
        /// </summary>
        /// <param name="maskKey">The 4 byte mask key</param>
        /// <param name="payload">The payload to mutate</param>
        public static void ToggleMask(ArraySegment<byte> maskKey, ArraySegment<byte> payload)
        {
            if (maskKey.Count != MaskKeyLength)
            {
                throw new Exception($"MaskKey key must be {MaskKeyLength} bytes");
            }

            byte[] buffer = payload.Array;
            byte[] maskKeyArray = maskKey.Array;
            int payloadOffset = payload.Offset;
            int payloadCount = payload.Count;
            int maskKeyOffset = maskKey.Offset;

            // apply the mask key (this is a reversible process so no need to copy the payload)
            // NOTE: this is a hot function
            // TODO: make this faster
            for (int i = payloadOffset; i < payloadCount; i++)
            {
                int payloadIndex = i - payloadOffset; // index should start at zero
                int maskKeyIndex = maskKeyOffset + (payloadIndex % MaskKeyLength);
                buffer[i] = (Byte)(buffer[i] ^ maskKeyArray[maskKeyIndex]);
            }
        }
    }
}
