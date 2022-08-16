using Poltergeist.PhantasmaLegacy.Cryptography;
using Poltergeist.PhantasmaLegacy.Numerics;

namespace Poltergeist.PhantasmaLegacy.Domain
{
    public interface IChain
    {
        string Name { get; }
        Address Address { get; }
        BigInteger Height { get; }
    }
}
