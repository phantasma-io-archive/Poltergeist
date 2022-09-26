using System.IO;
using System.Runtime.CompilerServices;

namespace Phantasma.Shared.Utils
{
    public static class Utils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToUInt32(this byte[] value, int startIndex)
        {
            var a = value[startIndex];
            startIndex++;
            var b = value[startIndex];
            startIndex++;
            var c = value[startIndex];
            startIndex++;
            var d = value[startIndex];
            startIndex++;
            return (uint)(a + (b << 8) + (c << 16) + (d << 24));
        }
    }
}