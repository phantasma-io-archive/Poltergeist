using System;
using Poltergeist.PhantasmaLegacy.Numerics;

namespace Poltergeist.PhantasmaLegacy.Cryptography
{
    public static class CryptoExtensions
    {
        internal static BigInteger NextBigInteger(int sizeInBits)
        {
            if (sizeInBits < 0)
                throw new ArgumentException("sizeInBits must be non-negative");
            if (sizeInBits == 0)
                return 0;

            var b = Entropy.GetRandomBytes(sizeInBits / 8 + 1);

            if (sizeInBits % 8 == 0)
                b[b.Length - 1] = 0;
            else
                b[b.Length - 1] &= (byte)((1 << sizeInBits % 8) - 1);

            return BigInteger.FromUnsignedArray(b, isPositive: true);
        }
    }
}
