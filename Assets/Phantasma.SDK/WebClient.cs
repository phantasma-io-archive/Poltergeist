using System;
using System.Collections;

using UnityEngine;
using UnityEngine.Networking;

using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using System.Text;
using System.Threading;

namespace Phantasma.SDK
{
    public static class WebClient
    {
        public static int NoTimeout = 0;
        public static int DefaultTimeout = 30;
        private static long requestNumber = 0;
        private static object requestNumberLock = new object();
        private static long GetNextRequestNumber()
        {
            lock (requestNumberLock)
            {
                if (requestNumber == Int64.MaxValue)
                    requestNumber = 0;
                else
                    requestNumber++;
            }

            return requestNumber;
        }
        public static IEnumerator RPCRequest(string url, string method, int timeout, int retriesOnNetworkError, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback,
                                            Action<DataNode> callback, params object[] parameters)
        {
            var paramData = DataNode.CreateArray("params");

            if (parameters != null && parameters.Length > 0)
            {
                foreach (var obj in parameters)
                {
                    paramData.AddField(null, obj);
                }
            }

            var jsonRpcData = DataNode.CreateObject(null);
            jsonRpcData.AddField("jsonrpc", "2.0");
            jsonRpcData.AddField("method", method);
            jsonRpcData.AddField("id", "1");
            jsonRpcData.AddNode(paramData);

            string json;

            try
            {
                json = JSONWriter.WriteToString(jsonRpcData);
            }
            catch (Exception e)
            {
                throw e;
            }

            var requestNumber = GetNextRequestNumber();
            Log.Write($"RPC request [{requestNumber}]\nurl: {url}\njson: {json}", Log.Level.Networking);

            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            DateTime startTime = DateTime.Now;

            UnityWebRequest request;
            for (; ; )
            {
                request = new UnityWebRequest(url, "POST");
                request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (timeout > 0)
                    request.timeout = timeout;

                yield return request.SendWebRequest();

                if (request.error == null || retriesOnNetworkError == 0)
                {
                    // success
                    break;
                }

                Log.Write($"RPC network error [{requestNumber}], {retriesOnNetworkError} retries left.", Log.Level.Networking);
                Thread.Sleep(1000);
                retriesOnNetworkError--;
            }

            TimeSpan responseTime = DateTime.Now - startTime;

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                Log.Write($"RPC error [{requestNumber}]\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{request.error}\n{request.error}\nIs connection error: {request.result == UnityWebRequest.Result.ConnectionError}\nIs protocol error: {request.result == UnityWebRequest.Result.ProtocolError}\nIs data processing error: {request.result == UnityWebRequest.Result.DataProcessingError}\nResponse code: {request.responseCode}", Log.Level.Networking);
                if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR, request.error + $"\nURL: {url}\nIs connection error: {request.result == UnityWebRequest.Result.ConnectionError}\nIs protocol error: {request.result == UnityWebRequest.Result.ProtocolError}\nIs data processing error: {request.result == UnityWebRequest.Result.DataProcessingError}\nResponse code: {request.responseCode}");
            }
            else
            {
                Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{request.downloadHandler.text}", Log.Level.Networking);
                DataNode root = null;

                try
                {
                    root = JSONReader.ReadFromString(request.downloadHandler.text);
                }
                catch (Exception e)
                {
                    Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nFailed to parse JSON: " + e.Message, Log.Level.Networking);
                    if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.FAILED_PARSING_JSON, "Failed to parse JSON: " + e.Message);
                    yield break;
                }

                if (root == null)
                {
                    Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nFailed to parse JSON", Log.Level.Networking);
                    if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.FAILED_PARSING_JSON, "failed to parse JSON");
                }
                else
                if (root.HasNode("error"))
                {
                    var errorDesc = root["error"].GetString("message");
                    Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nError node found: {errorDesc}", Log.Level.Networking);
                    if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, errorDesc);
                }
                else
                if (root.HasNode("result"))
                {
                    var result = root["result"];

                    if (result.HasNode("error"))
                    {
                        // This is incorrect way of RPC error reporting,
                        // but it happens sometimes and should be handeled at least for now.
                        var errorDesc = result.GetString("error");
                        Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nError node found (2): {errorDesc}", Log.Level.Networking);
                        if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, errorDesc);
                    }
                    else
                    {
                        callback(result);
                    }
                }
                else
                {
                    if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.MALFORMED_RESPONSE, "malformed response");
                }
            }

            yield break;
        }
        public static IEnumerator RPCRequestEx(string url, string method, int timeout, int retriesOnNetworkError, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback,
            Action<DataNode> callback, DataNode parametersNode)
        {
            var jsonRpcData = DataNode.CreateObject(null);
            jsonRpcData.AddField("jsonrpc", "2.0");
            jsonRpcData.AddField("method", method);
            jsonRpcData.AddField("id", "1");
            jsonRpcData.AddNode(parametersNode);

            string json;

            try
            {
                json = JSONWriter.WriteToString(jsonRpcData);
            }
            catch (Exception e)
            {
                throw e;
            }

            var requestNumber = GetNextRequestNumber();
            Log.Write($"RPC request [{requestNumber}]\nurl: {url}\njson: {json}", Log.Level.Networking);

            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            DateTime startTime = DateTime.Now;

            UnityWebRequest request;
            for (; ; )
            {
                request = new UnityWebRequest(url, "POST");
                request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                if (timeout > 0)
                    request.timeout = timeout;

                yield return request.SendWebRequest();

                if (request.error == null || retriesOnNetworkError == 0)
                {
                    // success
                    break;
                }

                Log.Write($"RPC network error [{requestNumber}], {retriesOnNetworkError} retries left.", Log.Level.Networking);
                Thread.Sleep(1000);
                retriesOnNetworkError--;
            }

            TimeSpan responseTime = DateTime.Now - startTime;

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                Log.Write($"RPC error [{requestNumber}]\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{request.error}\nIs connection error: {request.result == UnityWebRequest.Result.ConnectionError}\nIs protocol error: {request.result == UnityWebRequest.Result.ProtocolError}\nIs data processing error: {request.result == UnityWebRequest.Result.DataProcessingError}\nResponse code: {request.responseCode}", Log.Level.Networking);
                if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR, request.error + $"\nURL: {url}\nIs connection error: {request.result == UnityWebRequest.Result.ConnectionError}\nIs protocol error: {request.result == UnityWebRequest.Result.ProtocolError}\nIs data processing error: {request.result == UnityWebRequest.Result.DataProcessingError}\nResponse code: {request.responseCode}");
            }
            else
            {
                Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{request.downloadHandler.text}", Log.Level.Networking);
                DataNode root = null;

                try
                {
                    root = JSONReader.ReadFromString(request.downloadHandler.text);
                }
                catch(Exception e)
                {
                    Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nFailed to parse JSON: " + e.Message, Log.Level.Networking);
                    if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.FAILED_PARSING_JSON, "Failed to parse JSON: " + e.Message);
                    yield break;
                }

                if (root == null)
                {
                    Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nFailed to parse JSON", Log.Level.Networking);
                    if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.FAILED_PARSING_JSON, "failed to parse JSON");
                }
                else
                if (root.HasNode("error"))
                {
                    var errorDesc = root["error"].GetString("message");
                    Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nError node found: {errorDesc}", Log.Level.Networking);
                    if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, errorDesc);
                }
                else
                if (root.HasNode("result"))
                {
                    var result = root["result"];

                    if (result.HasNode("error"))
                    {
                        // This is incorrect way of RPC error reporting,
                        // but it happens sometimes and should be handeled at least for now.
                        var errorDesc = result.GetString("error");
                        Log.Write($"RPC response [{requestNumber}]\nurl: {url}\nError node found (2): {errorDesc}", Log.Level.Networking);
                        if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, errorDesc);
                    }
                    else
                    {
                        callback(result);
                    }
                }
                else
                {
                    if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.MALFORMED_RESPONSE, "malformed response");
                }
            }

            yield break;
        }
        public static IEnumerator RESTRequest(string url, int timeout, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, Action<DataNode> callback)
        {
            UnityWebRequest request;

            var requestNumber = GetNextRequestNumber();
            Log.Write($"REST request [{requestNumber}]\nurl: {url}", Log.Level.Networking);

            request = new UnityWebRequest(url, "GET");
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

            DateTime startTime = DateTime.Now;

            if (timeout > 0)
                request.timeout = timeout;
            
            yield return request.SendWebRequest();
            
            TimeSpan responseTime = DateTime.Now - startTime;

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                Log.Write($"REST error [{requestNumber}]\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{request.error}\nIs connection error: {request.result == UnityWebRequest.Result.ConnectionError}\nIs protocol error: {request.result == UnityWebRequest.Result.ProtocolError}\nIs data processing error: {request.result == UnityWebRequest.Result.DataProcessingError}\nResponse code: {request.responseCode}", Log.Level.Networking);
                if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR, request.error + $"\nURL: {url}\nIs connection error: {request.result == UnityWebRequest.Result.ConnectionError}\nIs protocol error: {request.result == UnityWebRequest.Result.ProtocolError}\nIs data processing error: {request.result == UnityWebRequest.Result.DataProcessingError}\nResponse code: {request.responseCode}");
            }
            else
            {
                DataNode root = null;
                try
                {
                    Log.Write($"REST response [{requestNumber}]\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{request.downloadHandler.text}", Log.Level.Networking);
                    root = JSONReader.ReadFromString(request.downloadHandler.text);
                }
                catch (Exception e)
                {
                    Log.Write(e.Message);
                }
                callback(root);
            }

            yield break;
        }

        public static IEnumerator RESTRequest(string url, string serializedJson, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, Action<DataNode> callback)
        {
            UnityWebRequest request;

            var requestNumber = GetNextRequestNumber();
            Log.Write($"REST request (POST) [{requestNumber}]\nurl: {url}", Log.Level.Networking);

            Log.Write($"REST request (POST) [{requestNumber}]\nserializedJson: {serializedJson}", Log.Level.Debug1);

            request = new UnityWebRequest(url, "POST");

            byte[] data = Encoding.UTF8.GetBytes(serializedJson);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(data);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            DateTime startTime = DateTime.Now;
            yield return request.SendWebRequest();
            TimeSpan responseTime = DateTime.Now - startTime;

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                Log.Write($"REST error [{requestNumber}]\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{request.error}\nIs connection error: {request.result == UnityWebRequest.Result.ConnectionError}\nIs protocol error: {request.result == UnityWebRequest.Result.ProtocolError}\nIs data processing error: {request.result == UnityWebRequest.Result.DataProcessingError}\nResponse code: {request.responseCode}", Log.Level.Networking);
                if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR, request.error + $"\nURL: {url}\nIs connection error: {request.result == UnityWebRequest.Result.ConnectionError}\nIs protocol error: {request.result == UnityWebRequest.Result.ProtocolError}\nIs data processing error: {request.result == UnityWebRequest.Result.DataProcessingError}\nResponse code: {request.responseCode}");
            }
            else
            {
                DataNode root = null;
                try
                {
                    Log.Write($"REST response [{requestNumber}]\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{request.downloadHandler.text}", Log.Level.Networking);
                    root = JSONReader.ReadFromString(request.downloadHandler.text);
                }
                catch(Exception e)
                {
                    Log.Write(e.Message);
                }
                callback(root);
            }

            yield break;
        }

        public static IEnumerator Ping(string url, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, Action<TimeSpan> callback)
        {
            UnityWebRequest request;

            Log.Write($"Ping url: {url}", Log.Level.Networking);

            request = new UnityWebRequest(url, "GET");
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

            DateTime startTime = DateTime.Now;
            yield return request.SendWebRequest();
            TimeSpan responseTime = DateTime.Now - startTime;

            if(request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                Log.Write($"Ping error\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{request.error}\nIs connection error: {request.result == UnityWebRequest.Result.ConnectionError}\nIs protocol error: {request.result == UnityWebRequest.Result.ProtocolError}\nIs data processing error: {request.result == UnityWebRequest.Result.DataProcessingError}\nResponse code: {request.responseCode}", Log.Level.Networking);
                if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR, request.error + $"\nURL: {url}\nIs connection error: {request.result == UnityWebRequest.Result.ConnectionError}\nIs protocol error: {request.result == UnityWebRequest.Result.ProtocolError}\nIs data processing error: {request.result == UnityWebRequest.Result.DataProcessingError}\nResponse code: {request.responseCode}");
            }
            else
            {
                Log.Write($"Ping response\nurl: {url}\nResponse time: {responseTime.Seconds}.{responseTime.Milliseconds} sec\n{request.downloadHandler.text}", Log.Level.Networking);
                callback(responseTime);
            }

            yield break;
        }
    }
}