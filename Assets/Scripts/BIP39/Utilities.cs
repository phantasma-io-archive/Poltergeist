using System;
using System.Collections;
using System.Linq;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using System.IO;
using System.Threading.Tasks;


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
        /// Calculates RIPEMD160(SHA256(input)). This is used in Address calculations.
        /// </summary>
        public static byte[] Sha256Hash160(byte[] input)
        {
            var sha256 = new Sha256Digest();
            var digest = new RipeMD160Digest();
            sha256.BlockUpdate(input, 0, input.Length);
            var @out256 = new byte[sha256.GetDigestSize()];
            sha256.DoFinal(@out256, 0);
            digest.BlockUpdate(@out256, 0, @out256.Length);
            var @out = new byte[digest.GetDigestSize()];
            digest.DoFinal(@out, 0);
            return @out;
        }

        /// <summary>
        /// Calculates the SHA256 32 byte checksum of the input bytes
        /// </summary>
        /// <param name="input">bytes input to get checksum</param>
        /// <param name="offset">where to start calculating checksum</param>
        /// <param name="length">length of the input bytes to perform checksum on</param>
        /// <returns>32 byte array checksum</returns>
        public static byte[] Sha256Digest(byte[] input, int offset, int length)
        {
            var algorithm = new Sha256Digest();
            Byte[] firstHash = new Byte[algorithm.GetDigestSize()];
            algorithm.BlockUpdate(input, offset, length);
            algorithm.DoFinal(firstHash, 0);
            return firstHash;
        }

        /// <summary>
        /// Calculates the SHA512 64 byte checksum of the input bytes
        /// </summary>
        /// <param name="input">bytes input to get checksum</param>
        /// <param name="offset">where to start calculating checksum</param>
        /// <param name="length">length of the input bytes to perform checksum on</param>
        /// <returns>64 byte array checksum</returns>
        public static byte[] Sha512Digest(byte[] input, int offset, int length)
        {
            var algorithm = new Sha512Digest();
            Byte[] firstHash = new Byte[algorithm.GetDigestSize()];
            algorithm.BlockUpdate(input, offset, length);
            algorithm.DoFinal(firstHash, 0);
            return firstHash;
        }

        /// <summary>
        /// See <see cref="DoubleDigest(byte[], int, int)"/>.
        /// </summary>
        public static byte[] DoubleDigest(byte[] input)
        {
            return DoubleDigest(input, 0, input.Length);
        }

        /// <summary>
        /// Calculates the SHA-256 hash of the given byte range, and then hashes the resulting hash again. This is
        /// standard procedure in BitCoin. The resulting hash is in big endian form.
        /// </summary>
        public static byte[] DoubleDigest(byte[] input, int offset, int length)
        {
            var algorithm = new Sha256Digest();
            Byte[] firstHash = new Byte[algorithm.GetDigestSize()];
            algorithm.BlockUpdate(input, offset, length);
            algorithm.DoFinal(firstHash, 0);
            Byte[] secondHash = new Byte[algorithm.GetDigestSize()];
            algorithm.BlockUpdate(firstHash, 0, firstHash.Length);
            algorithm.DoFinal(secondHash, 0);
            return secondHash;
        }

        /// <summary>
        /// Calculates SHA256(SHA256(byte range 1 + byte range 2)).
        /// </summary>
        public static byte[] DoubleDigestTwoBuffers(byte[] input1, int offset1, int length1, byte[] input2, int offset2, int length2)
        {
            var algorithm = new Sha256Digest();
            var buffer = new byte[length1 + length2];
            Array.Copy(input1, offset1, buffer, 0, length1);
            Array.Copy(input2, offset2, buffer, length1, length2);
            Byte[] first = new Byte[algorithm.GetDigestSize()];
            algorithm.DoFinal(first, 0);
            algorithm.BlockUpdate(first, 0, first.Length);
            Byte[] output = new Byte[algorithm.GetDigestSize()];
            algorithm.DoFinal(output, 0);
            return output;
        }

        // The representation of nBits uses another home-brew encoding, as a way to represent a large
        // hash value in only 32 bits.
        public static BigInteger DecodeCompactBits(long compact)
        {
            var size = (byte)(compact >> 24);
            var bytes = new byte[4 + size];
            bytes[3] = size;
            if (size >= 1) bytes[4] = (byte)(compact >> 16);
            if (size >= 2) bytes[5] = (byte)(compact >> 8);
            if (size >= 3) bytes[6] = (byte)(compact >> 0);
            return DecodeMpi(bytes);
        }

        /// <summary>
        /// MPI encoded numbers are produced by the OpenSSL BN_bn2mpi function. They consist of
        /// a 4 byte big endian length field, followed by the stated number of bytes representing
        /// the number in big endian format.
        /// </summary>
        private static BigInteger DecodeMpi(byte[] mpi)
        {
            var length = ReadUint32Be(mpi, 0);
            var buf = new byte[length];
            Array.Copy(mpi, 4, buf, 0, (int)length);
            return new BigInteger(1, buf);
        }

        /// <summary>
        /// Converts a hex based string into its bytes contained in a byte array
        /// </summary>
        /// <param name="hex">The hex encoded string</param>
        /// <returns>the bytes derived from the hex encoded string</returns>
        public static byte[] HexStringToBytes(string hexString)
        {
            return Enumerable.Range(0, hexString.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(hexString.Substring(x, 2), 16)).ToArray();
        }

        // <summary>
        /// Turns a byte array into a Hex encoded string
        /// </summary>
        /// <param name="bytes">The bytes to encode to hex</param>
        /// <returns>The hex encoded representation of the bytes</returns>
        public static string BytesToHexString(byte[] bytes, bool upperCase = false)
        {
            if (upperCase)
            {
                return string.Concat(bytes.Select(byteb => byteb.ToString("X2")).ToArray());
            }
            else
            {
                return string.Concat(bytes.Select(byteb => byteb.ToString("x2")).ToArray());
            }
        }

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
        /// Safely get Crypto Random byte array at the size you desire.
        /// </summary>
        /// <param name="size">Size of the crypto random byte array to build</param>
        /// <param name="seedStretchingIterations">Optional parameter to specify how many SHA512 passes occur over our seed before we use it. Higher value is greater security but uses more computational power. If random byte generation is taking too long try specifying values lower than the default of 5000. You can set 0 to turn off stretching</param>
        /// <returns>A byte array of completely random bytes</returns>
        public static byte[] GetRandomBytes(int size, int seedStretchingIterations = 5000)
        {
            //varies from system to system, a tiny amount of entropy, tiny
            int processorCount = System.Environment.ProcessorCount;

            //another tiny amount of entropy due to the varying nature of thread id
            int currentThreadId = System.Environment.CurrentManagedThreadId;

            //a GUID is considered unique so also provides some entropy
            byte[] guidBytes = Guid.NewGuid().ToByteArray();

            //this combined with DateTime.Now is the default seed in BouncyCastles SecureRandom
            byte[] threadedSeedBytes = new ThreadedSeedGenerator().GenerateSeed(24, true);

            byte[] output = new byte[size];

            //if for whatever reason it says 0 or less processors just make it 16
            if (processorCount <= 0)
            {
                processorCount = 16;
            }

            //if some fool trys to set stretching to < 0 we protect them from themselves
            if (seedStretchingIterations < 0)
            {
                seedStretchingIterations = 0;
            }

            //we create a SecureRandom based off SHA256 just to get a random int which will be used to determine what bytes to "take" from our built seed hash and then rehash those taken seed bytes using a KDF (key stretching) such that it would slow down anyone trying to rebuild private keys from common seeds.
            SecureRandom seedByteTakeDetermine = SecureRandom.GetInstance("SHA256PRNG");

            guidBytes = HmacSha512Digest(guidBytes, 0, guidBytes.Length, MergeByteArrays(threadedSeedBytes, UTF8Encoding.UTF8.GetBytes(Convert.ToString(System.Environment.TickCount))));

            try
            {
                seedByteTakeDetermine.SetSeed(((DateTime.Now.Ticks - System.Environment.TickCount) * processorCount) + currentThreadId);
                seedByteTakeDetermine.SetSeed(guidBytes);
                seedByteTakeDetermine.SetSeed(seedByteTakeDetermine.GenerateSeed(1 + currentThreadId));
                seedByteTakeDetermine.SetSeed(threadedSeedBytes);
            }
            catch
            {
                try
                {
                    //if the number is too big or causes an error or whatever we will failover to this, as it's not our main source of random bytes and not used in the KDF stretching it's ok.
                    seedByteTakeDetermine.SetSeed((DateTime.Now.Ticks - System.Environment.TickCount) + currentThreadId);
                    seedByteTakeDetermine.SetSeed(guidBytes);
                    seedByteTakeDetermine.SetSeed(seedByteTakeDetermine.GenerateSeed(1 + currentThreadId));
                    seedByteTakeDetermine.SetSeed(threadedSeedBytes);
                }
                catch
                {
                    //if again the number is too big or causes an error or whatever we will failover to this, as it's not our main source of random bytes and not used in the KDF stretching it's ok.
                    seedByteTakeDetermine.SetSeed(DateTime.Now.Ticks - System.Environment.TickCount);
                    seedByteTakeDetermine.SetSeed(guidBytes);
                    seedByteTakeDetermine.SetSeed(seedByteTakeDetermine.GenerateSeed(1 + currentThreadId));
                    seedByteTakeDetermine.SetSeed(threadedSeedBytes);
                }
            }

            //hardened seed
            byte[] toHashForSeed;

            try
            {
                toHashForSeed = BitConverter.GetBytes(((processorCount - seedByteTakeDetermine.Next(0, processorCount)) * System.Environment.TickCount) * currentThreadId);
            }
            catch
            {
                try
                {
                    //if the number was too large or something we failover to this
                    toHashForSeed = BitConverter.GetBytes(((processorCount - seedByteTakeDetermine.Next(0, processorCount)) + System.Environment.TickCount) * currentThreadId);
                }
                catch
                {
                    //if the number was again too large or something we failover to this
                    toHashForSeed = BitConverter.GetBytes(((processorCount - seedByteTakeDetermine.Next(0, processorCount)) + System.Environment.TickCount) + currentThreadId);
                }
            }

            toHashForSeed = Sha512Digest(toHashForSeed, 0, toHashForSeed.Length);
            toHashForSeed = MergeByteArrays(toHashForSeed, guidBytes);
            toHashForSeed = MergeByteArrays(toHashForSeed, BitConverter.GetBytes(currentThreadId));
            toHashForSeed = MergeByteArrays(toHashForSeed, BitConverter.GetBytes(DateTime.UtcNow.Ticks));
            toHashForSeed = MergeByteArrays(toHashForSeed, BitConverter.GetBytes(DateTime.Now.Ticks));
            toHashForSeed = MergeByteArrays(toHashForSeed, BitConverter.GetBytes(System.Environment.TickCount));
            toHashForSeed = MergeByteArrays(toHashForSeed, BitConverter.GetBytes(processorCount));
            toHashForSeed = MergeByteArrays(toHashForSeed, threadedSeedBytes);
            toHashForSeed = Sha512Digest(toHashForSeed, 0, toHashForSeed.Length);

            //we grab a random amount of bytes between 24 and 64 to rehash  make a new set of 64 bytes, using guidBytes as hmackey
            toHashForSeed = Sha512Digest(HmacSha512Digest(toHashForSeed, 0, seedByteTakeDetermine.Next(24, 64), guidBytes), 0, 64);

            seedByteTakeDetermine.SetSeed(currentThreadId + (DateTime.Now.Ticks - System.Environment.TickCount));

            //by making the iterations also random we are again making it hard to determin our seed by brute force
            int iterations = seedStretchingIterations - (seedByteTakeDetermine.Next(0, (seedStretchingIterations / seedByteTakeDetermine.Next(9, 100))));

            //here we use key stretching techniques to make it harder to replay the random seed values by forcing computational time up
            byte[] seedMaterial = Rfc2898_pbkdf2_hmacsha512.PBKDF2(toHashForSeed, seedByteTakeDetermine.GenerateSeed(64), iterations);

            //build a SecureRandom object that uses Sha512 to provide randomness and we will give it our created above hardened seed
            SecureRandom secRand = new SecureRandom(new Org.BouncyCastle.Crypto.Prng.DigestRandomGenerator(new Sha512Digest()));

            //set the seed that we created just above
            secRand.SetSeed(seedMaterial);

            //generate more seed materisal
            secRand.SetSeed(currentThreadId);
            secRand.SetSeed(MergeByteArrays(guidBytes, threadedSeedBytes));
            secRand.SetSeed(secRand.GenerateSeed(1 + secRand.Next(64)));

            //add our prefab seed again onto the previous material just to be sure the above statements are adding and not clobbering seed material
            secRand.SetSeed(seedMaterial);

            //here we derive our random bytes
            secRand.NextBytes(output, 0, size);

            return output;
        }

        /// <summary>
        /// Safely get Crypto Random byte array at the size you desire, made this async version because can take 500ms to complete and so this allows non-blocking for the 500ms.
        /// </summary>
        /// <param name="size">Size of the crypto random byte array to build</param>
        /// <param name="seedStretchingIterations">Optional parameter to specify how many SHA512 passes occur over our seed before we use it. Higher value is greater security but uses more computational power. If random byte generation is taking too long try specifying values lower than the default of 5000. You can set 0 to turn off stretching</param>
        /// <returns>A byte array of completely random bytes</returns>
        public async static Task<byte[]> GetRandomBytesAsync(int size, int seedStretchingIterations = 5000)
        {
            return await Task.Run<byte[]>(() => GetRandomBytes(size, seedStretchingIterations));
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
        /// This switches the Endianess of the provided byte array, byte per byte we do bit swappy.
        /// </summary>
        /// <param name="bytes">Bytes to change endianess of</param>
        /// <returns>Bytes with endianess swapped</returns>
        public static byte[] SwapEndianBytes(byte[] bytes)
        {
            byte[] output = new byte[bytes.Length];

            int index = 0;

            foreach (byte b in bytes)
            {
                byte[] ba = { b };
                BitArray bits = new BitArray(ba);

                int newByte = 0;
                if (bits.Get(7)) newByte++;
                if (bits.Get(6)) newByte += 2;
                if (bits.Get(5)) newByte += 4;
                if (bits.Get(4)) newByte += 8;
                if (bits.Get(3)) newByte += 16;
                if (bits.Get(2)) newByte += 32;
                if (bits.Get(1)) newByte += 64;
                if (bits.Get(0)) newByte += 128;

                output[index] = Convert.ToByte(newByte);

                index++;
            }

            //I love lamp
            return output;
        }

        /// <summary>
        /// Returns a Positive BouncyCastle BigInteger
        /// </summary>
        /// <param name="bytes">Bytes to create BigInteger</param>
        /// <returns>A Positive BigInteger</returns>
        public static BigInteger NewPositiveBigInteger(byte[] bytes)
        {
            return new BigInteger(1, bytes);
        }

        /// <summary>
        /// Convert a .NET DateTime into a Unix Epoch represented time
        /// </summary>
        /// <param name="time">DateTime to convert</param>
        /// <returns>Number of ticks since the Unix Epoch</returns>
        public static ulong ToUnixTime(DateTime time)
        {
            return (ulong)(time.ToUniversalTime() - Globals.UnixEpoch).TotalSeconds;
        }

        /// <summary>
        /// Checks to see if supplied time is within the 70 minute tollerance for network error
        /// </summary>
        /// <param name="peerUnixTime">Unix time to check within threshold</param>
        /// <param name="timeOffset">Offset which is difference between peerUnixTime and local UTC time</param>
        /// <returns>Compliance within threshold</returns>
        public static bool UnixTimeWithin70MinuteThreshold(ulong peerUnixTime, out long timeOffset)
        {
            int maxOffset = 42000;
            int minOffset = -42000;

            ulong currentTime = ToUnixTime(DateTime.UtcNow);

            timeOffset = (Convert.ToInt64(currentTime)) - (Convert.ToInt64(peerUnixTime));

            if (timeOffset > maxOffset || timeOffset < minOffset)
            {
                return false;
            }

            return true;
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

        /// <summary>
        /// Uint32 to Byte Array in Little Endian
        /// </summary>
        /// <param name="val">the uint32 to convert</param>
        /// <param name="@out">The byte array representation of uint32 in little endian</param>
        /// <param name="offset">Offset to start placing the bytes in the byte array</param>
        public static void Uint32ToByteArrayLe(uint val, byte[] @out, int offset)
        {
            @out[offset + 0] = (byte)(val >> 0);
            @out[offset + 1] = (byte)(val >> 8);
            @out[offset + 2] = (byte)(val >> 16);
            @out[offset + 3] = (byte)(val >> 24);
        }

        /// <summary>
        /// Converts a Uint32 into a Stream of Bytes in Little Endian
        /// </summary>
        /// <param name="val">Uint32 to make stream</param>
        /// <param name="stream">Uint32 outout as byte stream little endian</param>
        public static void Uint32ToByteStreamLe(uint val, Stream stream)
        {
            stream.Write((new byte[] { (byte)(val >> 0) }), 0, (new byte[] { (byte)(val >> 0) }).Length);
            stream.Write((new byte[] { (byte)(val >> 8) }), 0, (new byte[] { (byte)(val >> 8) }).Length);
            stream.Write((new byte[] { (byte)(val >> 16) }), 0, (new byte[] { (byte)(val >> 16) }).Length);
            stream.Write((new byte[] { (byte)(val >> 24) }), 0, (new byte[] { (byte)(val >> 24) }).Length);
        }

        /// <summary>
        /// Converts a Uint32 into a Byte Array in Big Endian
        /// </summary>
        /// <param name="val">Uint32 to convert</param>
        /// <param name="@out">Byte array that will contain the result of the conversion</param>
        /// <param name="offset">Offset in byte array to start placing output</param>
        public static void Uint32ToByteArrayBe(uint val, byte[] @out, int offset)
        {
            @out[offset + 0] = (byte)(val >> 24);
            @out[offset + 1] = (byte)(val >> 16);
            @out[offset + 2] = (byte)(val >> 8);
            @out[offset + 3] = (byte)(val >> 0);
        }

        /// <summary>
        /// Convert a ulong to byte stream little endian
        /// </summary>
        /// <param name="val">ulong for conversion</param>
        /// <param name="stream">byte stream of ulong in little endian order</param>
        public static void Uint64ToByteStreamLe(ulong val, Stream stream)
        {
            var bytes = BitConverter.GetBytes(val);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            stream.Write(bytes, 0, bytes.Length);
        }
        /// <summary>
        /// Bytes to Uint32
        /// </summary>
        /// <param name="bytes">Bytes to get Uint32 from</param>
        /// <param name="offset">Offset to start getting the UInt32 from</param>
        /// <returns>Uint32</returns>
        public static uint ReadUint32(byte[] bytes, int offset)
        {
            return (((uint)bytes[offset + 0]) << 0) |
                   (((uint)bytes[offset + 1]) << 8) |
                   (((uint)bytes[offset + 2]) << 16) |
                   (((uint)bytes[offset + 3]) << 24);
        }

        /// <summary>
        /// Bytes to Uint32 in BigEndian format
        /// </summary>
        /// <param name="bytes">Bytes to get Uint32 from</param>
        /// <param name="offset">Offset to start getting the UInt32 from</param>
        /// <returns>Uint32</returns>
        public static uint ReadUint32Be(byte[] bytes, int offset)
        {
            return (((uint)bytes[offset + 0]) << 24) |
                   (((uint)bytes[offset + 1]) << 16) |
                   (((uint)bytes[offset + 2]) << 8) |
                   (((uint)bytes[offset + 3]) << 0);
        }

        /// <summary>
        /// Reverse the order of given Byte Array
        /// </summary>
        /// <param name="bytes">Byte array to reverse</param>
        /// <returns>reverse copy of supplied Byte array</returns>
        public static byte[] ReverseBytes(byte[] bytes)
        {
            var buf = new byte[bytes.Length];
            for (var i = 0; i < bytes.Length; i++)
                buf[i] = bytes[bytes.Length - 1 - i];
            return buf;
        }
    }
}
