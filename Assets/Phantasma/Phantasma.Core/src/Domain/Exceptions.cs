using System;

//TODO
namespace Poltergeist.PhantasmaLegacy.Domain
{
    public class ChainException : Exception
    {
        public ChainException(string msg) : base(msg)
        {

        }
    }
}
