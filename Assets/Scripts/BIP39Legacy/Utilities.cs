using System;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto;


namespace Bitcoin.BIP39
{
    /// <summary>
    /// A Library that provides common functionality between my other Bitcoin Modules
    /// Made by thashiznets@yahoo.com.au
    /// v1.0.0.2
    /// Bitcoin:1ETQjMkR1NNh4jwLuN5LxY7bMsHC9PUPSV
    /// </summary>
    public static class Utilities
    {
        /// <summary>
        /// Calculates the 64 byte checksum in accordance with HMAC-SHA512
        /// </summary>
        /// <param name="input">The bytes to derive the checksum from</param>
        /// <param name="offset">Where to start calculating checksum in the input bytes</param>
        /// <param name="length">Length of buytes to use to calculate checksum</param>
        /// <param name="hmacKey">HMAC Key used to generate the checksum (note differing HMAC Keys provide unique checksums)</param>
        /// <returns></returns>
        public static byte[] HmacSha512Digest(byte[] input, int offset, int length, byte[] hmacKey)
        {
            byte[] output = new byte[64];
            HMac _hmacsha512Obj;
            _hmacsha512Obj = new HMac(new Sha512Digest());
            ICipherParameters param = new Org.BouncyCastle.Crypto.Parameters.KeyParameter(hmacKey);
            _hmacsha512Obj.Init(param);
            _hmacsha512Obj.BlockUpdate(input, offset, length);
            _hmacsha512Obj.DoFinal(output, 0);
            return output;
        }

        /// <summary>
        /// Merges two byte arrays
        /// </summary>
        /// <param name="source1">first byte array</param>
        /// <param name="source2">second byte array</param>
        /// <returns>A byte array which contains source1 bytes followed by source2 bytes</returns>
        public static Byte[] MergeByteArrays(Byte[] source1, Byte[] source2)
        {
            //Most efficient way to merge two arrays this according to http://stackoverflow.com/questions/415291/best-way-to-combine-two-or-more-byte-arrays-in-c-sharp
            Byte[] buffer = new Byte[source1.Length + source2.Length];
            System.Buffer.BlockCopy(source1, 0, buffer, 0, source1.Length);
            System.Buffer.BlockCopy(source2, 0, buffer, source1.Length, source2.Length);

            return buffer;
        }

        /// <summary>
        /// Normalises a string with NKFD normal form
        /// </summary>
        /// <param name="toNormalise">String to be normalised</param>
        /// <returns>Normalised string</returns>
        public static String NormaliseStringNfkd(String toNormalise)
        {
            return toNormalise.Trim().Normalize(NormalizationForm.FormKD);
        }
    }
}
