using Poltergeist.PhantasmaLegacy.Cryptography;
using Poltergeist.PhantasmaLegacy.Numerics;
using Poltergeist.PhantasmaLegacy.Storage;
using Poltergeist.PhantasmaLegacy.VM;
using System;

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

        public static void Notify<T>(this IRuntime runtime, EventKind kind, Address address, T content)
        {
            var bytes = content == null ? new byte[0] : Serialization.Serialize(content);
            runtime.Notify(kind, address, bytes);
        }

        public static bool IsReadOnlyMode(this IRuntime runtime)
        {
            return runtime.Transaction == null;
        }
    }
}
