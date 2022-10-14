using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain
{
    public interface IArchive
    {
        string Name { get; }
        Hash Hash { get; }
        MerkleTree MerkleTree { get; }
        BigInteger Size { get; }
        Timestamp Time { get; }
        IArchiveEncryption Encryption { get; }
        BigInteger BlockCount { get; }
        IEnumerable<Address> Owners { get; }
        int OwnerCount { get; }
        IEnumerable<int> MissingBlockIndices { get; }
        int MissingBlockCount { get; }
        IEnumerable<Hash> BlockHashes { get; }
        void SerializeData(BinaryWriter writer);
        byte[] ToByteArray();
        void UnserializeData(BinaryReader reader);
        void AddOwner(Address address);
        void RemoveOwner(Address address);
        bool IsOwner(Address address);
        void AddMissingBlock(int blockIndex);
    }
}
