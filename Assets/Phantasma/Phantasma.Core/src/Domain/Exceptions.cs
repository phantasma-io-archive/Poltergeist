using System;
using Phantasma.Core.Cryptography;

//TODO
namespace Phantasma.Core.Domain
{
    public class ChainException : Exception
    {
        public ChainException(string msg) : base(msg)
        {

        }
    }
}
