using System;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Numerics;

namespace Phantasma.Core.Domain
{
    public static class DomainExtensions
    {
        public static T GetContent<T>(this Event evt)
        {
            return Serialization.Unserialize<T>(evt.Data);
        }
    }
}
