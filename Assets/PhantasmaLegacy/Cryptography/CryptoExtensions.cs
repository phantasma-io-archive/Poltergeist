using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Poltergeist.PhantasmaLegacy.Numerics;

namespace Poltergeist.PhantasmaLegacy.Cryptography
{
    public static class CryptoExtensions
    {
        public static byte[] AESGenerateIV(int vectorSize)
        {
            var ivBytes = new byte[vectorSize];
            var secRandom = new Org.BouncyCastle.Security.SecureRandom();
            secRandom.NextBytes(ivBytes);

            return ivBytes;
        }

        public static byte[] AESGCMDecrypt(byte[] data, byte[] key, byte[] iv)
        {
            var keyParamWithIV = new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), iv, 0, 16);

            var cipher = Org.BouncyCastle.Security.CipherUtilities.GetCipher("AES/GCM/NoPadding");
            cipher.Init(false, keyParamWithIV);

            return cipher.DoFinal(data);
        }

        public static byte[] AESGCMEncrypt(byte[] data, byte[] key, byte[] iv)
        {
            var keyParamWithIV = new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), iv, 0, 16);

            var cipher = Org.BouncyCastle.Security.CipherUtilities.GetCipher("AES/GCM/NoPadding");
            cipher.Init(true, keyParamWithIV);

            return cipher.DoFinal(data);
        }

        public static byte[] AESGCMDecrypt(byte[] data, byte[] key)
        {
            byte[] iv;
            byte[] encryptedData;

            using (var stream = new System.IO.MemoryStream(data))
            {
                using (var reader = new System.IO.BinaryReader(stream))
                {
                    iv = reader.ReadBytes(16);
                    encryptedData = reader.ReadBytes(data.Length - 16);
                }
            }
            
            var keyParamWithIV = new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), iv, 0, 16);

            var cipher = Org.BouncyCastle.Security.CipherUtilities.GetCipher("AES/GCM/NoPadding");
            cipher.Init(false, keyParamWithIV);

            return cipher.DoFinal(encryptedData);
        }

        public static byte[] AESGCMEncrypt(byte[] data, byte[] key)
        {
            byte[] iv = AESGenerateIV(16);
            var keyParamWithIV = new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), iv, 0, 16);

            var cipher = Org.BouncyCastle.Security.CipherUtilities.GetCipher("AES/GCM/NoPadding");
            cipher.Init(true, keyParamWithIV);

            var encryptedData = cipher.DoFinal(data);

            using (var stream = new System.IO.MemoryStream())
            {
                using (var writer = new System.IO.BinaryWriter(stream))
                {
                    writer.Write(iv);
                    writer.Write(encryptedData);
                }

                return stream.ToArray();
            }
        }

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

        public static byte[] SHA256(this IEnumerable<byte> value)
        {
            return new Hashing.SHA256().ComputeHash(value.ToArray());
        }

        public static byte[] Sha256(this string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return bytes.SHA256();
        }

        public static byte[] Sha256(this byte[] value)
        {
            return new Hashing.SHA256().ComputeHash(value, 0, (uint)value.Length);
        }

        public static byte[] Sha256(this byte[] value, uint offset, uint count)
        {
            return new Hashing.SHA256().ComputeHash(value, offset, count);
        }
    }
}
