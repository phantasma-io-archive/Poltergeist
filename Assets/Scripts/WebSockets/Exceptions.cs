using System;
using System.Collections.Generic;
using System.Text;

namespace LunarLabs.WebServer.Websockets
{
    public class EntityTooLargeException : Exception
    {
        public EntityTooLargeException() : base()
        {

        }

        /// <summary>
        /// Http header too large to fit in buffer
        /// </summary>
        public EntityTooLargeException(string message) : base(message)
        {

        }

        public EntityTooLargeException(string message, Exception inner) : base(message, inner)
        {

        }
    }

    public class InvalidHttpResponseCodeException : Exception
    {
        public string ResponseCode { get; private set; }

        public string ResponseHeader { get; private set; }

        public string ResponseDetails { get; private set; }

        public InvalidHttpResponseCodeException() : base()
        {
        }

        public InvalidHttpResponseCodeException(string message) : base(message)
        {
        }

        public InvalidHttpResponseCodeException(string responseCode, string responseDetails, string responseHeader) : base(responseCode)
        {
            ResponseCode = responseCode;
            ResponseDetails = responseDetails;
            ResponseHeader = responseHeader;
        }

        public InvalidHttpResponseCodeException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    public class SecWebSocketKeyMissingException : Exception
    {
        public SecWebSocketKeyMissingException() : base()
        {

        }

        public SecWebSocketKeyMissingException(string message) : base(message)
        {

        }

        public SecWebSocketKeyMissingException(string message, Exception inner) : base(message, inner)
        {

        }
    }

    public class ServerListenerSocketException : Exception
    {
        public ServerListenerSocketException() : base()
        {
        }

        public ServerListenerSocketException(string message) : base(message)
        {
        }

        public ServerListenerSocketException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    public class WebSocketBufferOverflowException : Exception
    {
        public WebSocketBufferOverflowException() : base()
        {
        }

        public WebSocketBufferOverflowException(string message) : base(message)
        {
        }

        public WebSocketBufferOverflowException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    public class WebSocketHandshakeFailedException : Exception
    {
        public WebSocketHandshakeFailedException() : base()
        {
        }

        public WebSocketHandshakeFailedException(string message) : base(message)
        {
        }

        public WebSocketHandshakeFailedException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    public class WebSocketVersionNotSupportedException : Exception
    {
        public WebSocketVersionNotSupportedException() : base()
        {
        }

        public WebSocketVersionNotSupportedException(string message) : base(message)
        {
        }

        public WebSocketVersionNotSupportedException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
