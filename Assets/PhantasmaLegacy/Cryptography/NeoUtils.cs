using Poltergeist.PhantasmaLegacy.Cryptography;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Poltergeist.PhantasmaLegacy.Neo2
{
    public static class NeoUtils
    {
        public static string ReverseHex(this string hex)
        {

            string result = "";
            for (var i = hex.Length - 2; i >= 0; i -= 2)
            {
                result += hex.Substring(i, 2);
            }
            return result;
        }

        public static bool IsValidAddress(this string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return false;
            }

            // In Norway "Aa" combination means "å".
            // By default StartsWith() uses current culture for comparison,
            // and if culture is set to Norway, StartWith() believes, that
            // Neo address "Aa..." starts with "å" letter,
            // and address is treated as invalid.
            // We should always use invariant culture for such comparisons.
            if (!address.StartsWith("A", false, CultureInfo.InvariantCulture))
            {
                return false;
            }

            if (address.Length != 34)
            {
                return false;
            }

            byte[] buffer;
            try
            {
                buffer = Poltergeist.PhantasmaLegacy.Numerics.Base58.Decode(address);

            }
            catch
            {
                return false;
            }

            if (buffer.Length < 4) return false;

            byte[] checksum = buffer.Sha256(0, (uint)buffer.Length - 4).Sha256();
            return buffer.Skip(buffer.Length - 4).SequenceEqual(checksum.Take(4));
        }

        public static byte[] ReadVarBytes(this BinaryReader reader, int max = 0X7fffffc7)
        {
            var len = (int)reader.ReadVarInt((ulong)max);
            if (len == 0) return null;
            return reader.ReadBytes(len);
        }

        public static ulong ReadVarInt(this BinaryReader reader, ulong max = ulong.MaxValue)
        {
            byte fb = reader.ReadByte();
            ulong value;
            if (fb == 0xFD)
                value = reader.ReadUInt16();
            else if (fb == 0xFE)
                value = reader.ReadUInt32();
            else if (fb == 0xFF)
                value = reader.ReadUInt64();
            else
                value = fb;
            if (value > max) throw new FormatException();
            return value;
        }

        public static void WriteVarBytes(this BinaryWriter writer, byte[] value)
        {
            if (value == null)
            {
                writer.WriteVarInt(0);
                return;
            }
            writer.WriteVarInt(value.Length);
            writer.Write(value);
        }

        public static void WriteVarInt(this BinaryWriter writer, long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException();
            if (value < 0xFD)
            {
                writer.Write((byte)value);
            }
            else if (value <= 0xFFFF)
            {
                writer.Write((byte)0xFD);
                writer.Write((ushort)value);
            }
            else if (value <= 0xFFFFFFFF)
            {
                writer.Write((byte)0xFE);
                writer.Write((uint)value);
            }
            else
            {
                writer.Write((byte)0xFF);
                writer.Write(value);
            }
        }

        public static void WriteFixed(this BinaryWriter writer, decimal value)
        {
            long D = 100000000;
            value *= D;
            writer.Write((long)value);
        }

        public static byte[] GetScriptHashFromAddress(this string address)
        {
            var temp = address.Base58CheckDecode();
            temp = temp.SubArray(1, 20);
            return temp;
        }

        public static string ByteToHex(this byte[] data)
        {
            string hex = BitConverter.ToString(data).Replace("-", "").ToLower();
            return hex;
        }

        public static string ToHexString(this IEnumerable<byte> value)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in value)
                sb.AppendFormat("{0:x2}", b);
            return sb.ToString();
        }

        public static byte[] HexToBytes(this string value)
        {
            if (value == null || value.Length == 0)
                return new byte[0];
            if (value.Length % 2 == 1)
                throw new FormatException();

            if (value.StartsWith("0x"))
            {
                value = value.Substring(2);
            }

            byte[] result = new byte[value.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = byte.Parse(value.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier);
            return result;
        }

        private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static uint ToTimestamp(this DateTime time)
        {
            return (uint)(time.ToUniversalTime() - unixEpoch).TotalSeconds;
        }
    }

}
