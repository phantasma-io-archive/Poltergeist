using Poltergeist.PhantasmaLegacy.Cryptography;

namespace Poltergeist.PhantasmaLegacy.Domain
{
    public struct PlatformSwapAddress
    {
        public string ExternalAddress;
        public Address LocalAddress;
    }

    public interface IPlatform
    {
        string Name { get; }
        string Symbol { get; } // for fuel
        PlatformSwapAddress[] InteropAddresses { get; }
    }
}
