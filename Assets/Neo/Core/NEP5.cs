using System;
using System.Collections;
using System.Numerics;
using Phantasma.SDK;
using Poltergeist.PhantasmaLegacy.Cryptography;
using Poltergeist.Neo2.Utils;

namespace Poltergeist.Neo2.Core
{
    public class NEP5
    {
        public readonly UInt160 ScriptHash;
        public readonly NeoAPI api;

        public NEP5(NeoAPI api, string contractHash) : this(api, NeoAPI.GetScriptHashFromString(contractHash))
        {

        }

        public NEP5(NeoAPI api, byte[] contractHash) : this(api, new UInt160(contractHash))
        {

        }

        public NEP5(NeoAPI api, UInt160 contractHash)
        {
            this.api = api;
            this.ScriptHash = contractHash;
        }

        public IEnumerator Transfer(UnspentEntries unspent, NeoKeys from_key, string to_address, BigInteger amount, string interop, decimal fee, Action<Transaction, string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback)
        {
            return Transfer(unspent, from_key, to_address.GetScriptHashFromAddress(), amount, interop, fee, callback, errorHandlingCallback);
        }
        public IEnumerator Transfer(UnspentEntries unspent, NeoKeys from_key, byte[] to_address_hash, BigInteger amount, string interop, decimal fee, Action<Transaction, string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback)
        {
            var sender_address_hash = from_key.Address.GetScriptHashFromAddress();
            return api.CallContract(callback, errorHandlingCallback, unspent, from_key, ScriptHash, "transfer", new object[] { sender_address_hash, to_address_hash, amount}, interop, fee);
        }
    }
}
