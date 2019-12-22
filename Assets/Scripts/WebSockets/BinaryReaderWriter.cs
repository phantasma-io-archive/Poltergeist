using System;
using System.IO;

namespace LunarLabs.WebSockets
{
    internal class BinaryReaderWriter
    {
        public static void ReadExactly(int length, Stream stream, ArraySegment<byte> buffer)
        {
            if (length == 0)
            {
                return;
            }

            if (buffer.Count < length)
            {
                // This will happen if the calling function supplied a buffer that was too small to fit the payload of the websocket frame.
                // Note that this can happen on the close handshake where the message size can be larger than the regular payload
                throw new InternalBufferOverflowException($"Unable to read {length} bytes into buffer (offset: {buffer.Offset} size: {buffer.Count}). Use a larger read buffer");
            }

            int offset = 0;
            do
            {
                int bytesRead = stream.Read(buffer.Array, buffer.Offset + offset, length - offset);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException(string.Format("Unexpected end of stream encountered whilst attempting to read {0:#,##0} bytes", length));
                }

                offset += bytesRead;
            } while (offset < length);

            return;
        }

        public static ushort ReadUShortExactly(Stream stream, bool isLittleEndian, ArraySegment<byte> buffer)
        {
            ReadExactly(2, stream, buffer);

            if (!isLittleEndian)
            {
                Array.Reverse(buffer.Array, buffer.Offset, 2); // big endian
            }

            return BitConverter.ToUInt16(buffer.Array, buffer.Offset);
        }

        public static ulong ReadULongExactly(Stream stream, bool isLittleEndian, ArraySegment<byte> buffer)
        {
            ReadExactly(8, stream, buffer);

            if (!isLittleEndian)
            {
                Array.Reverse(buffer.Array, buffer.Offset, 8); // big endian
            }

            return BitConverter.ToUInt64(buffer.Array, buffer.Offset);
        }

        public static long ReadLongExactly(Stream stream, bool isLittleEndian, ArraySegment<byte> buffer)
        {
            ReadExactly(8, stream, buffer);

            if (!isLittleEndian)
            {
                Array.Reverse(buffer.Array, buffer.Offset, 8); // big endian
            }

            return BitConverter.ToInt64(buffer.Array, buffer.Offset);
        }

        public static void WriteInt(int value, Stream stream, bool isLittleEndian)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian && !isLittleEndian)
            {
                Array.Reverse(buffer);
            }

            stream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteULong(ulong value, Stream stream, bool isLittleEndian)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian && ! isLittleEndian)
            {
                Array.Reverse(buffer);
            }

            stream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteLong(long value, Stream stream, bool isLittleEndian)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian && !isLittleEndian)
            {
                Array.Reverse(buffer);
            }

            stream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteUShort(ushort value, Stream stream, bool isLittleEndian)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian && !isLittleEndian)
            {
                Array.Reverse(buffer);
            }

            stream.Write(buffer, 0, buffer.Length);
        }
    }
}
