using System;
using System.Linq;

namespace Bitcoin.BIP39
{
    /// <summary>
    /// Implementation of the Rfc2898 PBKDF2 specification located here http://www.ietf.org/rfc/rfc2898.txt using HMACSHA512 but modified as opposed to PWDTKto match the BIP39 test vectors
    /// Using BouncyCastle for the HMAC-SHA512 instead of Microsoft implementation
    /// NOTE NOT IDENTICLE TO PWDTK (PWDTK is concatenating password and salt together before hashing the concatenated byte block, this is simply hashing the salt as what we are told to do in BIP39, yes the mnemonic sentence is provided as the hmac key)
    /// Created by thashiznets@yahoo.com.au
    /// v1.1.0.0
    /// Bitcoin:1ETQjMkR1NNh4jwLuN5LxY7bbip39HC9PUPSV
    /// </summary>
    public class Rfc2898_pbkdf2_hmacsha512
    {
        #region Private Attributes

        //I made the variable names match the definition in RFC2898 - PBKDF2 where possible, so you can trace the code functionality back to the specification
        private readonly Byte[] P;
        private readonly Byte[] S;
        private readonly Int32 c;
        private Int32 dkLen;

        #endregion

        #region Public Constants

        public const int CMinIterations = 2048;
        //Minimum recommended salt length in Rfc2898
        public const int CMinSaltLength = 8;
        //Length of the Hash Digest Output - 512 bits - 64 bytes
        public const int hLen = 64;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor to create Rfc2898_pbkdf2_hmacsha512 object ready to perform Rfc2898 PBKDF2 functionality
        /// </summary>
        /// <param name="password">The Password to be hashed and is also the HMAC key</param>
        /// <param name="salt">Salt to be concatenated with the password</param>
        /// <param name="iterations">Number of iterations to perform HMACSHA Hashing for PBKDF2</param>
        public Rfc2898_pbkdf2_hmacsha512(Byte[] password, Byte[] salt, int iterations = CMinIterations)
        {
            P = password;
            S = salt;
            c = iterations;
        }

        #endregion

        #region Public Members And Static Methods
        /// <summary>
        /// Derive Key Bytes using PBKDF2 specification listed in Rfc2898 and HMACSHA512 as the underlying PRF (Psuedo Random Function)
        /// </summary>
        /// <param name="keyLength">Length in Bytes of Derived Key</param>
        /// <returns>Derived Key</returns>
        public Byte[] GetDerivedKeyBytes_PBKDF2_HMACSHA512(Int32 keyLength)
        {
            //no need to throw exception for dkLen too long as per spec because dkLen cannot be larger than Int32.MaxValue so not worth the overhead to check
            dkLen = keyLength;

            Double l = Math.Ceiling((Double)dkLen / hLen);

            Byte[] finalBlock = new Byte[0];

            for (Int32 i = 1; i <= l; i++)
            {
                //Concatenate each block from F into the final block (T_1..T_l)
                finalBlock = Utilities.MergeByteArrays(finalBlock, F(P, S, c, i));
            }

            //returning DK note r not used as dkLen bytes of the final concatenated block returned rather than <0...r-1> substring of final intermediate block + prior blocks as per spec
            return finalBlock.Take(dkLen).ToArray();

        }

        /// <summary>
        /// A static publicly exposed version of GetDerivedKeyBytes_PBKDF2_HMACSHA512 which matches the exact specification in Rfc2898 PBKDF2 using HMACSHA512
        /// </summary>
        /// <param name="P">Password passed as a Byte Array</param>
        /// <param name="S">Salt passed as a Byte Array</param>
        /// <param name="c">Iterations to perform the underlying PRF over</param>
        /// <param name="dkLen">Length of Bytes to return, an AES 256 key wold require 32 Bytes</param>
        /// <returns>Derived Key in Byte Array form ready for use by chosen encryption function</returns>
        public static Byte[] PBKDF2(Byte[] P, Byte[] S, int c = CMinIterations, int dkLen = hLen)
        {
            Rfc2898_pbkdf2_hmacsha512 rfcObj = new Rfc2898_pbkdf2_hmacsha512(P, S, c);
            return rfcObj.GetDerivedKeyBytes_PBKDF2_HMACSHA512(dkLen);
        }

        #endregion

        #region Private Members
        //Main Function F as defined in Rfc2898 PBKDF2 spec
        private Byte[] F(Byte[] P, Byte[] S, Int32 c, Int32 i)
        {

            //Salt and Block number Int(i) concatenated as per spec
            Byte[] Si = Utilities.MergeByteArrays(S, INT(i));

            //Initial hash (U_1) using password and salt concatenated with Int(i) as per spec
            Byte[] temp = PRF(Si, P);

            //Output block filled with initial hash value or U_1 as per spec
            Byte[] U_c = temp;

            for (Int32 C = 1; C < c; C++)
            {
                //rehashing the password using the previous hash value as salt as per spec
                temp = PRF(temp, P);

                for (Int32 j = 0; j < temp.Length; j++)
                {
                    //xor each byte of the each hash block with each byte of the output block as per spec
                    U_c[j] ^= temp[j];
                }
            }

            //return a T_i block for concatenation to create the final block as per spec
            return U_c;
        }

        //PRF function as defined in Rfc2898 PBKDF2 spec
        private Byte[] PRF(Byte[] S, Byte[] hmacKey)
        {
            //HMACSHA512 Hashing, better than the HMACSHA1 in Microsofts implementation ;)
            return Utilities.HmacSha512Digest(S, 0, S.Count(), hmacKey);
        }

        //This method returns the 4 octet encoded Int32 with most significant bit first as per spec
        private Byte[] INT(Int32 i)
        {
            Byte[] I = BitConverter.GetBytes(i);

            //Make sure most significant bit is first
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(I);
            }

            return I;
        }

        #endregion
    }
}
