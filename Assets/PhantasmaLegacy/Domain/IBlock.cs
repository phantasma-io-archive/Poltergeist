using Poltergeist.PhantasmaLegacy.Core.Types;
using Poltergeist.PhantasmaLegacy.Cryptography;
using Poltergeist.PhantasmaLegacy.Numerics;

namespace Poltergeist.PhantasmaLegacy.Domain
{
    public interface IOracleEntry
    {
        string URL { get; }
        byte[] Content { get; }
    }

    public interface IBlock
    {
        Address ChainAddress { get; }
        BigInteger Height { get; }
        Timestamp Timestamp { get; }
        Hash PreviousHash { get; }
        uint Protocol { get; }
        Hash Hash { get; }
        Hash[] TransactionHashes { get; }
        IOracleEntry[] OracleData { get; }
    }
}
