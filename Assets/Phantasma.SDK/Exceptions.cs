using System;

namespace Phantasma.SDK
{
    public class PhantasmaSDKException : Exception
    {
        public EPHANTASMA_SDK_ERROR_TYPE Type;
        public PhantasmaSDKException(EPHANTASMA_SDK_ERROR_TYPE type, string message) : base(message)
        {
            Type = type;
        }
    }
}