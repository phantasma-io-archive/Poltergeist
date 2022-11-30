using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace LunarLabs.WebServer.HTTP
{
    public enum HTTPCode
    {
        OK = 200,
        Redirect = 302, //https://en.wikipedia.org/wiki/HTTP_302
        NotModified = 304,
        BadRequest = 400,
        Unauthorized = 401,
        Forbidden = 403,
        NotFound = 404,
        InternalServerError = 500,
        ServiceUnavailable = 503,
    }

    public class HTTPRequest
    {
        public enum Method
        {
            Get,
            Post,
            Head,
            Put,
            Delete
        }

        public Method method;
        public string url;
        public string path;
        public string version;

        public byte[] bytes;

        public string postBody;

        public Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public Dictionary<string, string> args = new Dictionary<string, string>();

        public bool HasVariable(string name)
        {
            return args != null && args.ContainsKey(name);
        }

        public string GetVariable(string name)
        {
            if (HasVariable(name))
            {
                return args[name];
            }

            return null;
        }

        private HTTPCode DecodeStatusCode(string code)
        {
            throw new NotImplementedException();
        }

        private void AddDefaultHeader(string key, string val)
        {
            if (headers.ContainsKey(key))
            {
                return;
            }

            headers[key] = val;
        }
    }
}
