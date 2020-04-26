using System;
using Nethereum.JsonRpc.Client.RpcMessages;
using Phantasma.SDK;

namespace Nethereum.JsonRpc.Client
{

    public class RpcLogger
    {
        public RpcLogger()
        {
        }
        public string RequestJsonMessage { get; private set; }
        public RpcResponseMessage ResponseMessage { get; private set; }

        public void LogRequest(string requestJsonMessage)
        {
            RequestJsonMessage = requestJsonMessage;
            Log.Write(GetRPCRequestLogMessage());
        }

        private string GetRPCRequestLogMessage()
        {
            return $"RPC Request: {RequestJsonMessage}";
        }

        private string GetRPCResponseLogMessage()
        {
            return ResponseMessage != null ? $"RPC Response: {ResponseMessage.Result}" : String.Empty;
        }


        public void LogResponse(RpcResponseMessage responseMessage)
        {
            ResponseMessage = responseMessage;

            Log.Write(GetRPCResponseLogMessage());

            if (HasError(responseMessage))
            {
                Log.WriteError($"RPC Response Error: {responseMessage.Error.Message}");
            }
        }

        public void LogException(Exception ex)
        {
            Log.WriteError("RPC Exception, "  + GetRPCRequestLogMessage() + GetRPCResponseLogMessage() +"\n"+ ex);
        }

        private bool HasError(RpcResponseMessage message)
        {
            return message.Error != null && message.HasError;
        }

    }

}