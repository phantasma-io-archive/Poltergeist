using Poltergeist.PhantasmaLegacy.Storage;

namespace Poltergeist.PhantasmaLegacy.Domain
{
    public static class DomainExtensions
    {
        public static T GetContent<T>(this Event evt)
        {
            return Serialization.Unserialize<T>(evt.Data);
        }

        public static string GetContractName(this NativeContractKind nativeContract)
        {
            return nativeContract.ToString().ToLower();
        }
    }
}
