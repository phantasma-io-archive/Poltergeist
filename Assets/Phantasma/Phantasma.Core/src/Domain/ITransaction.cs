using Phantasma.Core.Cryptography;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain
{
    public interface ITransaction
    {
        byte[] Script { get; }

        string NexusName { get; }
        string ChainName { get; }

        Timestamp Expiration { get; }

        byte[] Payload { get; }

        Signature[] Signatures { get; }
        Hash Hash { get; }
    }
}
