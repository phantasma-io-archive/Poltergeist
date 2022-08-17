using Poltergeist.PhantasmaLegacy.Cryptography;
using Poltergeist.PhantasmaLegacy.Storage;

namespace Poltergeist.PhantasmaLegacy.Domain
{
    public enum ArchiveEncryptionMode
    {
        None,
        Private,
        Shared
    }

    public interface IArchiveEncryption: ISerializable
    {
        ArchiveEncryptionMode Mode { get; }

        string EncryptName(string name, PhantasmaKeys keys);
        string DecryptName(string name, PhantasmaKeys keys);
        byte[] Encrypt(byte[] chunk, PhantasmaKeys keys);
        byte[] Decrypt(byte[] chunk, PhantasmaKeys keys);
    }
}
