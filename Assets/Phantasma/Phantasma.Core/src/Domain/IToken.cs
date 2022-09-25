using System;

namespace Poltergeist.PhantasmaLegacy.Domain
{
    [Flags]
    public enum TokenFlags
    {
        None = 0,
        Transferable = 1 << 0,
        Fungible = 1 << 1,
        Finite = 1 << 2,
        Divisible = 1 << 3,
        Fuel = 1 << 4,
        Stakable = 1 << 5,
        Fiat = 1 << 6,
        Swappable = 1 << 7,
        Burnable = 1 << 8,
    }
}
