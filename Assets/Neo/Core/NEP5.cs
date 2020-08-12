using System;
using System.Collections;
using System.Numerics;
using Phantasma.Neo.Cryptography;
using Phantasma.Neo.Utils;

namespace Phantasma.Neo.Core
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

        public IEnumerator GetName(Action<string> callback)
        {
            return api.InvokeScript(ScriptHash, "name", new object[] { "" }, (response) =>
            {
                var name = response.result.GetString();
                callback(name);
            });
        }

        public IEnumerator GetSymbol(Action<string> callback)
        {
            return api.InvokeScript(ScriptHash, "symbol", new object[] { "" }, (response) =>
            {
                var symbol = response.result.GetString();
                callback(symbol);
            });
        }

        public IEnumerator GetDecimals(Action<int> callback)
        {
            return api.InvokeScript(ScriptHash, "decimals", new object[] { "" }, (response) =>
            {
                var decimals = response.result.GetBigInteger();
                callback((int)decimals);
            });
        }

        public IEnumerator GetTotalSupply(Action<int> callback)
        {
            return api.InvokeScript(ScriptHash, "totalSupply", new object[] { "" }, (response) =>
            {
                var supply = response.result.GetBigInteger();
                callback((int)supply);
            });
        }

        public IEnumerator BalanceOf(string address, Action<BigInteger> callback)
        {
            return BalanceOf(address.GetScriptHashFromAddress(), callback);
        }

        public IEnumerator BalanceOf(NeoKeys keys, Action<BigInteger> callback)
        {
            return BalanceOf(keys.Address, callback);
        }

        public IEnumerator BalanceOf(UInt160 hash, Action<BigInteger> callback)
        {
            return BalanceOf(hash.ToArray(), callback);
        }

        public IEnumerator BalanceOf(byte[] addressHash, Action<BigInteger> callback)
        {
            return api.InvokeScript(ScriptHash, "balanceOf", new object[] { addressHash }, (response) =>
            {
                var balances = response.result.GetBigInteger();
                callback(balances);
            });
        }

        public IEnumerator Transfer(UnspentEntries unspent, NeoKeys from_key, string to_address, BigInteger amount, string interop, decimal fee, Action<Transaction, string> callback)
        {
            return Transfer(unspent, from_key, to_address.GetScriptHashFromAddress(), amount, interop, fee, callback);
        }

        public IEnumerator Transfer(UnspentEntries unspent, NeoKeys from_key, UInt160 to_address_hash, BigInteger amount, string interop, decimal fee, Action<Transaction, string> callback)
        {
            return Transfer(unspent, from_key, to_address_hash.ToArray(), amount, interop, fee, callback);
        }

        public IEnumerator Transfer(UnspentEntries unspent, NeoKeys from_key, byte[] to_address_hash, BigInteger amount, string interop, decimal fee, Action<Transaction, string> callback)
        {
            var sender_address_hash = from_key.Address.GetScriptHashFromAddress();
            return api.CallContract(callback, unspent, from_key, ScriptHash, "transfer", new object[] { sender_address_hash, to_address_hash, amount}, interop, fee);
        }
    }
}
