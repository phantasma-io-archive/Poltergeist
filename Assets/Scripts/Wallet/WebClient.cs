﻿using System;
using System.Collections;

using UnityEngine;
using UnityEngine.Networking;

using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using System.Text;

namespace Phantasma.SDK
{
    public static class WebClient
    {
        public static IEnumerator RPCRequest(string url, string method, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback,
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

            UnityWebRequest request;
            string json;

            try
            {
                json = JSONWriter.WriteToString(jsonRpcData);
            }
            catch (Exception e)
            {
                throw e;
            }

            Debug.Log($"RPC request\nurl:{url}\njson: {json}");

            request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.Log(request.error);
                if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR, request.error);
            }
            else
            {
                Debug.Log(request.downloadHandler.text);
                var root = JSONReader.ReadFromString(request.downloadHandler.text);

                if (root == null)
                {
                    if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.FAILED_PARSING_JSON, "failed to parse JSON");
                }
                else
                if (root.HasNode("error"))
                {
                    var errorDesc = root["error"].GetString("message");
                    if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, errorDesc);
                }
                else
                if (root.HasNode("result"))
                {
                    var result = root["result"];
                    callback(result);
                }
                else
                {
                    if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.MALFORMED_RESPONSE, "malformed response");
                }
            }

            yield break;
        }

        public static IEnumerator RESTRequest(string url, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, Action<DataNode> callback)
        {
            UnityWebRequest request;

            Debug.Log($"REST request\nurl:{url}");

            request = new UnityWebRequest(url, "GET");
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.Log(request.error);
                if (errorHandlingCallback != null) errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR, request.error);
            }
            else
            {
                Debug.Log(request.downloadHandler.text);
                var root = JSONReader.ReadFromString(request.downloadHandler.text);
                callback(root);
            }

            yield break;
        }
    }
}