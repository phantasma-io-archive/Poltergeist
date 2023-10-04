using System;

namespace Poltergeist
{
    [Flags]
    public enum PlatformKind
    {
        None = 0x0,
        Phantasma = 0x1,
        Neo = 0x2,
        Ethereum = 0x4,
        BSC = 0x8,
    }
}
