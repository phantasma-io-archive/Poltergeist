using LunarLabs.Parser;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using Phantasma.Neo.Cryptography;
using Phantasma.Neo.Utils;
using Phantasma.Neo.VM.Types;
using Phantasma.SDK;

namespace Phantasma.Neo.Core
{
    public class NeoException : Exception
    {
        public NeoException(string msg) : base(msg)
        {

        }

        public NeoException(string msg, Exception cause) : base(msg, cause)
        {

        }
    }

    public enum VMType
    {
        Unknown,
        String,
        Boolean,
        Integer,
        Array,
        ByteArray
    }

    [Flags]
    public enum VMState : byte
    {
        NONE = 0,

        HALT = 1 << 0,
        FAULT = 1 << 1,
        BREAK = 1 << 2,
    }

    public class InvokeResult
    {
        public VMState state;
        public decimal gasSpent;
        public StackItem result;
        public Transaction transaction;
    }

    public struct UnspentEntry
    {
        public UInt256 hash;
        public uint index;
        public decimal value;
    }

    public class UnspentEntries
    {
        public Dictionary<string, List<UnspentEntry>> entries;
    }

    public class NeoAPI
    {
        public readonly string neoscanUrl;
        public readonly string neoRPCUrl;

        private static Dictionary<string, string> _systemAssets = null;

        private Action<string> _logger;
        public Action<string> Logger
        {
            get
            {
                return _logger != null ? _logger : DummyLogger;
            }
        }

        public NeoAPI(string neoRPCUrl, string neoscanURL) 
        {
            this.neoRPCUrl = neoRPCUrl;
            this.neoscanUrl = neoscanURL;
        }

        public virtual void SetLogger(Action<string> logger = null)
        {
            this._logger = logger;
        }

        private void DummyLogger(string s)
        {

        }

        internal static Dictionary<string, string> GetAssetsInfo()
        {
            if (_systemAssets == null)
            {
                _systemAssets = new Dictionary<string, string>();
                AddAsset("NEO", "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b");
                AddAsset("GAS", "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7");
            }

            return _systemAssets;
        }

        private static void AddAsset(string symbol, string hash)
        {
            _systemAssets[symbol] = hash;
        }

        public static byte[] GetAssetID(string symbol)
        {
            var info = GetAssetsInfo();
            foreach (var entry in info)
            {
                if (entry.Key == symbol)
                {
                    return NeoUtils.ReverseHex(entry.Value).HexToBytes();
                }
            }

            return null;
        }

        public static IEnumerable<KeyValuePair<string, string>> Assets
        {
            get
            {
                var info = GetAssetsInfo();
                return info;
            }
        }

        public static string SymbolFromAssetID(byte[] assetID)
        {
            var str = assetID.ByteToHex();
            var result = SymbolFromAssetID(str);
            if (result == null)
            {
                result = SymbolFromAssetID(NeoUtils.ReverseHex(str));
            }

            return result;
        }

        public static string SymbolFromAssetID(string assetID)
        {
            if (assetID == null)
            {
                return null;
            }

            if (assetID.StartsWith("0x"))
            {
                assetID = assetID.Substring(2);
            }

            var info = GetAssetsInfo();
            foreach (var entry in info)
            {
                if (entry.Value == assetID)
                {
                    return entry.Key;
                }
            }

            return null;
        }


        // TODO NEP5 should be refactored to be a data object without the embedded api

        public struct TokenInfo
        {
            public string symbol;
            public string hash;
            public string name;
            public int decimals;
        }

        private static Dictionary<string, TokenInfo> _tokenScripts = null;

        internal static Dictionary<string, TokenInfo> GetTokenInfo()
        {
            if (_tokenScripts == null)
            {
                _tokenScripts = new Dictionary<string, TokenInfo>();
                AddToken("RPX", "ecc6b20d3ccac1ee9ef109af5a7cdb85706b1df9", "RedPulse", 8);
                AddToken("DBC", "b951ecbbc5fe37a9c280a76cb0ce0014827294cf", "DeepBrain", 8);
                AddToken("QLC", "0d821bd7b6d53f5c2b40e217c6defc8bbe896cf5", "Qlink", 8);
                AddToken("APH", "a0777c3ce2b169d4a23bcba4565e3225a0122d95", "Aphelion", 8);
                AddToken("ZPT", "ac116d4b8d4ca55e6b6d4ecce2192039b51cccc5", "Zeepin", 8);
                AddToken("TKY", "132947096727c84c7f9e076c90f08fec3bc17f18", "TheKey", 8);
                AddToken("TNC", "08e8c4400f1af2c20c28e0018f29535eb85d15b6", "Trinity", 8);
                AddToken("CPX", "45d493a6f73fa5f404244a5fb8472fc014ca5885", "APEX", 8);
                AddToken("ACAT", "7f86d61ff377f1b12e589a5907152b57e2ad9a7a", "ACAT", 8);
                AddToken("NRVE", "a721d5893480260bd28ca1f395f2c465d0b5b1c2", "Narrative", 8);
                AddToken("THOR", "67a5086bac196b67d5fd20745b0dc9db4d2930ed", "Thor", 8);
                AddToken("RHT", "2328008e6f6c7bd157a342e789389eb034d9cbc4", "HashPuppy", 0);
                AddToken("IAM", "891daf0e1750a1031ebe23030828ad7781d874d6", "BridgeProtocol", 8);
                AddToken("SWTH", "ab38352559b8b203bde5fddfa0b07d8b2525e132", "Switcheo", 8);
                AddToken("OBT", "0e86a40588f715fcaf7acd1812d50af478e6e917", "Orbis", 8);
                AddToken("ONT", "ceab719b8baa2310f232ee0d277c061704541cfb", "Ontology", 8);
                AddToken("SOUL", "ed07cffad18f1308db51920d99a2af60ac66a7b3", "Phantasma", 8); //OLD 4b4f63919b9ecfd2483f0c72ff46ed31b5bbb7a4
                AddToken("AVA", "de2ed49b691e76754c20fe619d891b78ef58e537", "Travala", 8);
                AddToken("EFX", "acbc532904b6b51b5ea6d19b803d78af70e7e6f9", "Effect.AI", 8);
                AddToken("MCT", "a87cc2a513f5d8b4a42432343687c2127c60bc3f", "Master Contract", 8);
                AddToken("GDM", "d1e37547d88bc9607ff9d73116ebd9381c156f79", "Guardium", 8);
                AddToken("PKC", "af7c7328eee5a275a3bcaee2bf0cf662b5e739be", "Pikcio", 8);
                AddToken("ASA", "a58b56b30425d3d1f8902034996fcac4168ef71d", "Asura", 8);
                AddToken("LRN", "06fa8be9b6609d963e8fc63977b9f8dc5f10895f", "Loopring", 8);
                AddToken("TKY", "132947096727c84c7f9e076c90f08fec3bc17f18", "THEKEY", 8);
                AddToken("NKN", "c36aee199dbba6c3f439983657558cfb67629599", "NKN", 8);
                AddToken("XQT", "6eca2c4bd2b3ed97b2aa41b26128a40ce2bd8d1a", "Quarteria", 8);
                AddToken("EDS", "81c089ab996fc89c468a26c0a88d23ae2f34b5c0", "Endorsit Shares", 8);
            }

            return _tokenScripts;
        }

        private static void AddToken(string symbol, string hash, string name, int decimals)
        {
            _tokenScripts[symbol] = new TokenInfo { symbol = symbol, hash = hash, name = name, decimals = decimals };
        }

        public static IEnumerable<string> TokenSymbols
        {
            get
            {
                var info = GetTokenInfo();
                return info.Keys;
            }
        }

        public static byte[] GetScriptHashFromString(string hash)
        {
            hash = hash.ToLower();
            if (hash.StartsWith("0x"))
            {
                hash = hash.Substring(2);
            }

            return hash.HexToBytes().Reverse().ToArray();
        }

        public static byte[] GetScriptHashFromSymbol(string symbol)
        {
            GetTokenInfo();
            foreach (var entry in _tokenScripts)
            {
                if (entry.Key == symbol)
                {
                    return GetScriptHashFromString(entry.Value.hash);
                }
            }

            return null;
        }

        public static string GetStringFromScriptHash(byte[] hash)
        {
            return NeoUtils.ReverseHex(hash.ToHexString());
        }

        protected static StackItem ParseStack(DataNode stack)
        {
            if (stack != null)
            {
                //var items = new List<StackItem>();

                if (stack.Children.Count() > 0 && stack.Name == "stack")
                {
                    foreach (var child in stack.Children)
                    {
                        var item = ParseStackItems(child);
                        return item;
                        //items.Add(item);
                    }
                }

                return null;
                //return items.ToArray();
            }

            return null;
        }

        protected static StackItem ParseStackItems(DataNode stackItem)
        {
            var type = stackItem.GetString("type");
            var value = stackItem.GetString("value");

            switch (type)
            {
                case "ByteArray":
                    {
                        return new VM.Types.ByteArray(value.HexToBytes());
                    }

                case "Boolean":
                    {
                        return new VM.Types.Boolean(value.ToLower() == "true");
                    }

                case "Integer":
                    {
                        BigInteger intVal;
                        BigInteger.TryParse(value, out intVal);
                        return new VM.Types.Integer(intVal);
                    }
                case "Array": // Type
                    {
                        var items = new List<StackItem>();
                        foreach (var child in stackItem.Children)
                        {
                            var item = ParseStackItems(child);
                            items.Add(item);
                        }
                        return new VM.Types.Array(items);
                    }
                default:
                    {
                        //Console.WriteLine("ParseStack:unknown DataNode stack type: '" + type + "'");
                        break;
                    }
            }

            return null;
        }

        public static void EmitObject(ScriptBuilder sb, object item)
        {
            if (item is IEnumerable<byte>)
            {
                var arr = ((IEnumerable<byte>)item).ToArray();

                sb.EmitPush(arr);
            }
            else
            if (item is IEnumerable<object>)
            {
                var arr = ((IEnumerable<object>)item).ToArray();

                for (int index = arr.Length - 1; index >= 0; index--)
                {
                    EmitObject(sb, arr[index]);
                }

                sb.EmitPush(arr.Length);
                sb.Emit(OpCode.PACK);
            }
            else
            if (item == null)
            {
                sb.EmitPush("");
            }
            else
            if (item is string)
            {
                sb.EmitPush((string)item);
            }
            else
            if (item is bool)
            {
                sb.EmitPush((bool)item);
            }
            else
            if (item is BigInteger)
            {
                sb.EmitPush((BigInteger)item);
            }
            else
            if (item is UInt160)
            {
                sb.EmitPush(((UInt160)item).ToArray());
            }
            else
            if (item is UInt256)
            {
                sb.EmitPush(((UInt256)item).ToArray());
            }
            else
            if (item is int || item is sbyte || item is short)
            {
                var n = (int)item;
                sb.EmitPush((BigInteger)n);
            }
            else
            if (item is uint || item is byte || item is ushort)
            {
                var n = (uint)item;
                sb.EmitPush((BigInteger)n);
            }
            else
            {
                throw new NeoException("Unsupported contract parameter: " + item.ToString());
            }
        }

        public static byte[] GenerateScript(UInt160 scriptHash, object[] args, bool addNonce = true)
        {
            using (var sb = new ScriptBuilder())
            {
                var items = new Stack<object>();

                if (args != null)
                {
                    foreach (var item in args)
                    {
                        items.Push(item);
                    }
                }

                while (items.Count > 0)
                {
                    var item = items.Pop();
                    EmitObject(sb, item);
                }

                sb.EmitAppCall(scriptHash, false);

                if (addNonce)
                {
                    var timestamp = DateTime.UtcNow.ToTimestamp();
                    var nonce = BitConverter.GetBytes(timestamp);

                    //sb.Emit(OpCode.THROWIFNOT);
                    sb.Emit(OpCode.RET);
                    sb.EmitPush(nonce);
                }

                var bytes = sb.ToArray();

                string hex = bytes.ByteToHex();
                //System.IO.File.WriteAllBytes(@"D:\code\Crypto\neo-debugger-tools\ICO-Template\bin\Debug\inputs.avm", bytes);

                return bytes;
            }
        }

        private Dictionary<string, Transaction> lastTransactions = new Dictionary<string, Transaction>();

        public void GenerateInputsOutputs(UnspentEntries unspent, NeoKeys key, string symbol, IEnumerable<Transaction.Output> targets, out List<Transaction.Input> inputs, out List<Transaction.Output> outputs, decimal system_fee = 0, bool allowSameSourceAndDest = false)
        {
            var from_script_hash = new UInt160(key.signatureHash.ToArray());
            var info = GetAssetsInfo();
            var targetAssetID = NeoUtils.ReverseHex(info[symbol]).HexToBytes();
            if (targets != null)
                foreach (var t in targets)
                    if (t.assetID == null)
                        t.assetID = targetAssetID;
            //else Console.WriteLine("ASSETID target already existed: " + symbol);
            GenerateInputsOutputs(unspent, from_script_hash, targets, out inputs, out outputs, system_fee, allowSameSourceAndDest);
        }

        public void GenerateInputsOutputs(UnspentEntries unspent, UInt160 key, string symbol, IEnumerable<Transaction.Output> targets, out List<Transaction.Input> inputs, out List<Transaction.Output> outputs, decimal system_fee = 0, bool allowSameSourceAndDest = false)
        {
            var info = GetAssetsInfo();
            var targetAssetID = NeoUtils.ReverseHex(info[symbol]).HexToBytes();
            if (targets != null)
                foreach (var t in targets)
                    if (t.assetID == null)
                        t.assetID = targetAssetID;
            // else  Console.WriteLine("ASSETID target already existed: " + symbol);
            GenerateInputsOutputs(unspent, key, targets, out inputs, out outputs, system_fee, allowSameSourceAndDest);
        }

        public void GenerateInputsOutputs(UnspentEntries unspent, NeoKeys key, IEnumerable<Transaction.Output> targets, out List<Transaction.Input> inputs, out List<Transaction.Output> outputs, decimal system_fee = 0, bool allowSameSourceAndDest = false)
        {
            var from_script_hash = new UInt160(key.signatureHash.ToArray());
            GenerateInputsOutputs(unspent, from_script_hash, targets, out inputs, out outputs, system_fee);
        }

        public void GenerateInputsOutputs(UnspentEntries unspent, UInt160 from_script_hash, IEnumerable<Transaction.Output> targets, out List<Transaction.Input> inputs, out List<Transaction.Output> outputs, decimal system_fee = 0, bool allowSameSourceAndDest = false)
        {
            // filter any asset lists with zero unspent inputs
            var entries = unspent.entries.Where(pair => pair.Value.Count > 0).ToDictionary(pair => pair.Key, pair => pair.Value);

            inputs = new List<Transaction.Input>();
            outputs = new List<Transaction.Output>();

            var from_address = from_script_hash.ToAddress();
            var info = GetAssetsInfo();

            // dummy tx to self
            if (targets == null)
            {
                // We get here from CallContract() method.
                string assetName = "GAS";
                string assetID = info[assetName];
                var targetAssetID = NeoUtils.ReverseHex(assetID).HexToBytes();
                if (!entries.ContainsKey(assetName))
                    throw new NeoException($"Not enough {assetName} in address {from_address}");

                decimal selected = 0;
                if (system_fee == 0)
                {
                    // Taking any GAS unspent entry if no fee is set.
                    var src = entries[assetName][0];
                    selected = src.value;

                    inputs.Add(new Transaction.Input()
                    {
                        prevHash = src.hash,
                        prevIndex = src.index,
                    });
                }
                else
                {
                    // Taking ALL GAS unspent entries if fee is set and merging them.
                    foreach (var gasEntry in entries[assetName])
                    {
                        selected += gasEntry.value;
                        inputs.Add(new Transaction.Input()
                        {
                            prevHash = gasEntry.hash,
                            prevIndex = gasEntry.index,
                        });
                    }
                }
                // Console.WriteLine("SENDING " + selected + " GAS to source");

                outputs.Add(new Transaction.Output()
                {
                    assetID = targetAssetID,
                    scriptHash = from_script_hash,
                    value = selected - system_fee
                });
                return;
            }

            if(!allowSameSourceAndDest)
                foreach (var target in targets)
                    if (target.scriptHash.Equals(from_script_hash))
                        throw new NeoException("Target can't be same as input");

            bool done_fee = false;
            foreach (var asset in info)
            {
                string assetName = asset.Key;
                string assetID = asset.Value;

                if (!entries.ContainsKey(assetName))
                    continue;

                var targetAssetID = NeoUtils.ReverseHex(assetID).HexToBytes();

                var thistargets = targets.Where(o => o.assetID.SequenceEqual(targetAssetID));

                decimal cost = -1;
                foreach (var target in thistargets)
                    if (target.assetID.SequenceEqual(targetAssetID))
                    {
                        if (cost < 0)
                            cost = 0;
                        cost += target.value;
                    }

                // incorporate fee in GAS utxo, if sending GAS
                bool sendfee = false;
                if (system_fee > 0 && assetName == "GAS")
                {
                    done_fee = true;
                    sendfee = true;
                    if (cost < 0)
                        cost = 0;
                    cost += system_fee;
                }

                if (cost == -1)
                    continue;

                var sources = entries[assetName].OrderBy(src => src.value);
                decimal selected = 0;

                // >= cost ou > cost??
                foreach (var src in sources)
                {
                    if (selected >= cost && inputs.Count > 0)
                        break;

                    selected += src.value;
                    inputs.Add(new Transaction.Input()
                    {
                        prevHash = src.hash,
                        prevIndex = src.index,
                    });
                    // Console.WriteLine("ADD inp " + src.ToString());
                }

                if (selected < cost)
                    throw new NeoException($"Not enough {assetName} in address {from_address}");

                if (cost > 0)
                    foreach (var target in thistargets)
                        outputs.Add(target);

                if (selected > cost || cost == 0 || sendfee)  /// is sendfee needed? yes if selected == cost
                    outputs.Add(new Transaction.Output()
                    {
                        assetID = targetAssetID,
                        scriptHash = from_script_hash,
                        value = selected - cost
                    });
            }
        }

        public IEnumerator CallContract(Action<Transaction, string> callback, UnspentEntries unspent, NeoKeys key, UInt160 scriptHash, string operation, object[] args, string interop, decimal fee = 0, string attachSymbol = null, IEnumerable<Transaction.Output> attachTargets = null)
        {
            return CallContract(callback, unspent, key, scriptHash, new object[] { operation, args }, interop, fee, attachSymbol, attachTargets);
        }

        public IEnumerator CallContract(Action<Transaction, string> callback, UnspentEntries unspent, NeoKeys key, UInt160 scriptHash, object[] args, string interop, decimal fee = 0, string attachSymbol = null, IEnumerable<Transaction.Output> attachTargets = null)
        {
            var bytes = GenerateScript(scriptHash, args);
            return CallContract(callback, unspent, key, scriptHash, bytes, interop, fee, attachSymbol, attachTargets);
        }

        public IEnumerator CallContract(Action<Transaction, string> callback, UnspentEntries unspent, NeoKeys key, UInt160 scriptHash, byte[] bytes, string interop, decimal fee = 0, string attachSymbol = null, IEnumerable<Transaction.Output> attachTargets = null)
        {
            List<Transaction.Input> inputs = null;
            List<Transaction.Output> outputs = null;

            if (attachSymbol == null)
            {
                attachSymbol = "GAS";
            }   
            
            if (!string.IsNullOrEmpty(attachSymbol))
            {
                GenerateInputsOutputs(unspent, key, attachSymbol, attachTargets, out inputs, out outputs, fee);

                if (inputs.Count == 0)
                {
                    throw new NeoException($"Not enough inputs for transaction");
                }
            }

            var transaction = new Transaction()
            {
                type = TransactionType.InvocationTransaction,
                version = 0,
                script = bytes,
                gas = 0,
                inputs = inputs != null ? inputs.ToArray() : null,
                outputs = outputs != null ? outputs.ToArray() : null,
                attributes = inputs == null ? (new TransactionAttribute[] { new TransactionAttribute(TransactionAttributeUsage.Script, key.Address.AddressToScriptHash()) } ) : null,
                interop = interop
            };

            transaction.Sign(key);

            return SendTransaction(transaction, (error) =>
            {
                callback(string.IsNullOrEmpty(error) ? transaction : null, error);
            });
        }

        public IEnumerator GetTransaction(string hash, Action<Transaction> callback)
        {
            var val = new UInt256(NeoUtils.ReverseHex(hash).HexToBytes());
            return GetTransaction(val, callback);
        }

        public IEnumerator SendAsset(Action<Transaction, string> callback, UnspentEntries unspent, NeoKeys fromKey, string toAddress, string symbol, decimal amount, string interop, decimal fee = 0, bool allowSameSourceAndDest = false)
        {
            if (!allowSameSourceAndDest && String.Equals(fromKey.Address, toAddress, StringComparison.OrdinalIgnoreCase))
            {
                throw new NeoException("Source and dest addresses are the same");
            }

            var toScriptHash = toAddress.GetScriptHashFromAddress();
            var target = new Transaction.Output() { scriptHash = new UInt160(toScriptHash), value = amount };
            var targets = new List<Transaction.Output>() { target };
            return SendAsset(callback, unspent, fromKey, symbol, targets, interop, fee, allowSameSourceAndDest);
        }

        public IEnumerator SendAsset(Action<Transaction, string> callback, UnspentEntries unspent, NeoKeys fromKey, string symbol, IEnumerable<Transaction.Output> targets, string interop, decimal fee = 0, bool allowSameSourceAndDest = false)
        {
            List<Transaction.Input> inputs;
            List<Transaction.Output> outputs;

            GenerateInputsOutputs(unspent, fromKey, symbol, targets, out inputs, out outputs, fee, allowSameSourceAndDest);

            Transaction tx = new Transaction()
            {
                type = TransactionType.ContractTransaction,
                version = 0,
                script = null,
                gas = -1,
                inputs = inputs.ToArray(),
                outputs = outputs.ToArray(),
                interop = interop,
            };

            tx.Sign(fromKey);

            return SendTransaction(tx, (error) =>
            {
                callback(string.IsNullOrEmpty(error) ? tx : null, error);
            });
        }

        public IEnumerator ClaimGas(UnspentEntries unspent, NeoKeys ownerKey, List<UnspentEntry> claimable, decimal amount, Action<Transaction, string> callback)
        {
            var targetScriptHash = new UInt160(ownerKey.Address.AddressToScriptHash());

            var references = new List<Transaction.Input>();
            foreach (var entry in claimable)
            {
                references.Add(new Transaction.Input() { prevHash = entry.hash, prevIndex = entry.index });
            }

            if (amount <= 0)
            {
                throw new ArgumentException("No GAS to claim at this address");
            }

            List<Transaction.Output> outputs = new List<Transaction.Output>();

            outputs.Add(
            new Transaction.Output()
            {
                scriptHash = targetScriptHash,
                assetID = NeoAPI.GetAssetID("GAS"),
                value = amount
            });

            Transaction tx = new Transaction()
            {
                type = TransactionType.ClaimTransaction,
                version = 0,
                script = null,
                gas = -1,
                claimReferences = references.ToArray(),
                inputs = new Transaction.Input[0],
                outputs = outputs.ToArray(),
            };

            tx.Sign(ownerKey);

            return SendTransaction(tx, (error) =>
            {
                callback(string.IsNullOrEmpty(error) ? tx : null, error);
            });
        }

        public bool IsAsset(string symbol)
        {
            var info = GetAssetsInfo();
            return info.ContainsKey(symbol);
        }

        public bool IsToken(string symbol)
        {
            var info = GetTokenInfo();
            return info.ContainsKey(symbol);
        }

        public NEP5 GetToken(string symbol)
        {
            var info = GetTokenInfo();
            if (info.ContainsKey(symbol))
            {
                var token = info[symbol];
                return new NEP5(this, token.hash);
            }

            throw new NeoException("Invalid token symbol");
        }

        public IEnumerator GetUnspent(string address, Action<UnspentEntries> callback)
        {
            return GetUnspent(new UInt160(address.AddressToScriptHash()), callback);
        }

        private void LogData(DataNode node, int ident = 0)
        {
            var tabs = new string('\t', ident);
            Logger($"{tabs}{node}");
            foreach (DataNode child in node.Children)
                LogData(child, ident + 1);
        }

        public IEnumerator ExecuteRequestWeb(Action<DataNode> callback, Action<Phantasma.SDK.EPHANTASMA_SDK_ERROR_TYPE, string> onError, string url)
        {
            return WebClient.RESTRequest(url, onError, (response) => callback(response));
        }

        public IEnumerator ExecuteRequestRPC(Action<DataNode> callback, Action<Phantasma.SDK.EPHANTASMA_SDK_ERROR_TYPE, string> onError, string method, object[] _params)
        {
            return WebClient.RPCRequest(this.neoRPCUrl, method, onError, (response) => callback(response), _params);
        }

        private void ErrorHandler(Phantasma.SDK.EPHANTASMA_SDK_ERROR_TYPE type, string msg)
        {
            Log.WriteError(type + ": " + msg);
        }

        private void ErrorHandlerWithThrow(Phantasma.SDK.EPHANTASMA_SDK_ERROR_TYPE type, string msg)
        {
            // This handler uses Debug.Log() instead of Debug.LogError() to avoid getting to fatal error screen.
            Log.Write(type + ": " + msg);
            // We throw exception to be catched by StartThrowingCoroutine(),
            // so that it could be processed by calling code.
            throw new Exception(type + ": " + msg);
        }

        public IEnumerator GetAssetBalancesOf(NeoKeys key, Action<Dictionary<string, decimal>> callback)
        {
            return GetAssetBalancesOf(key.Address, callback);
        }

        public IEnumerator GetAssetBalancesOf(string address, Action<Dictionary<string, decimal>> callback)
        {
            var hash = new UInt160(address.AddressToScriptHash());
            return GetAssetBalancesOf(hash, callback);
        }


        public IEnumerator GetAssetBalancesOf(UInt160 scriptHash, Action<Dictionary<string, decimal>> callback)
        {
            return ExecuteRequestRPC(
                (response) => {
                    var result = new Dictionary<string, decimal>();

                    var balances = response.GetNode("balances");

                    foreach (var entry in balances.Children)
                    {
                        var assetID = entry.GetString("asset");
                        var amount = entry.GetDecimal("value");

                        var symbol = SymbolFromAssetID(assetID);

                        result[symbol] = amount;
                    }

                    callback(result);
                },
                ErrorHandler,
                "getaccountstate", new object[] { scriptHash.ToAddress() });
        }

        public IEnumerator GetStorage(string scriptHash, byte[] key, Action<byte[]> callback)
        {
            return ExecuteRequestRPC(
                (response) =>
                {
                    var result = response.GetString("result");
                    if (string.IsNullOrEmpty(result))
                    {
                        callback(null);
                    }
                    callback(result.HexToBytes());
                },
                ErrorHandler,
                "getstorage", new object[] { key.ByteToHex() });
        }

        // Note: This current implementation requires NeoScan running at port 4000
        public IEnumerator GetUnspent(UInt160 hash, Action<UnspentEntries> callback)
        {
            var url = this.neoscanUrl + "/api/main_net/v1/get_balance/" + hash.ToAddress();

            return ExecuteRequestWeb(
                (response) =>
                {
                    var unspents = new Dictionary<string, List<UnspentEntry>>();

                    response = response["balance"];

                    foreach (var child in response.Children)
                    {
                        var symbol = child.GetString("asset");

                        List<UnspentEntry> list = new List<UnspentEntry>();
                        unspents[symbol] = list;

                        var unspentNode = child.GetNode("unspent");
                        foreach (var entry in unspentNode.Children)
                        {
                            var txid = entry.GetString("txid");
                            var val = entry.GetDecimal("value");
                            var temp = new UnspentEntry() { hash = new UInt256(NeoUtils.ReverseHex(txid).HexToBytes()), value = val, index = entry.GetUInt32("n") };
                            list.Add(temp);
                        }
                    }

                    var result = new UnspentEntries()
                    {
                        entries = unspents
                    };
                    callback(result);
                },
                ErrorHandler,
                    url);

        }

        // Note: This current implementation requires NeoScan running at port 4000
        public IEnumerator GetClaimable(string address, Action<List<UnspentEntry>, decimal> callback)
        {
            var url = this.neoscanUrl + "/api/main_net/v1/get_claimable/" + address;
            return ExecuteRequestWeb(
                (response) =>
                {
                    var result = new List<UnspentEntry>();

                    var amount = response.GetDecimal("unclaimed");

                    response = response["claimable"];

                    foreach (var child in response.Children)
                    {
                        var txid = child.GetString("txid");
                        var index = child.GetUInt32("n");
                        var value = child.GetDecimal("unclaimed");

                        result.Add(new UnspentEntry() { hash = new UInt256(NeoUtils.ReverseHex(txid).HexToBytes()), index = index, value = value });
                    }

                    callback(result, amount);
                }
                , ErrorHandler, url);
        }

        public IEnumerator GetUnclaimed(string address, Action<decimal> callback)
        {
            var url = this.neoscanUrl + "/api/main_net/v1/get_unclaimed/" + address;
            return ExecuteRequestWeb(
                (response) =>
                {
                    var amount = response.GetDecimal("unclaimed");

                    callback(amount);
                }
                , ErrorHandler, url);
        }

        public IEnumerator SendRawTransaction(string hexTx, Action<string> callback)
        {
            return ExecuteRequestRPC((response) =>
            {
                try
                {
                    bool result;

                    if (response.HasNode("succeed"))
                    {
                        result = response.GetBool("succeed");
                    }
                    else
                    {
                        result = response.AsBool();
                    }

                    callback(result ? null :"sendrawtx rpc returned false");
                }
                catch (Exception e)
                {
                    callback(e.ToString());
                }
            },
            ErrorHandlerWithThrow, "sendrawtransaction", new object[] { hexTx });
        }

        protected IEnumerator SendTransaction(Transaction tx, Action<string> callback)
        {
            var rawTx = tx.Serialize(true);
            var hexTx = rawTx.ByteToHex();

            return SendRawTransaction(hexTx, callback);
        }

        public IEnumerator InvokeScript(UInt160 scriptHash, string operation, object[] args, Action<InvokeResult> callback)
        {
            return InvokeScript(scriptHash, new object[] { operation, args }, callback);
        }

        public IEnumerator InvokeScript(UInt160 scriptHash, object[] args, Action<InvokeResult> callback)
        {
            var script = GenerateScript(scriptHash, args);

            return InvokeScript(script, callback);
        }

        public IEnumerator InvokeScript(byte[] script, Action<InvokeResult> callback)
        {
            return ExecuteRequestRPC((response) =>
            {
                var invoke = new InvokeResult();
                invoke.state = VMState.NONE;

                if (response != null)
                {
                    var root = response["result"];
                    if (root != null)
                    {
                        var stack = root["stack"];
                        invoke.result = ParseStack(stack);

                        invoke.gasSpent = root.GetDecimal("gas_consumed");
                        var temp = root.GetString("state");

                        if (temp.Contains("FAULT"))
                        {
                            invoke.state = VMState.FAULT;
                        }
                        else
                        if (temp.Contains("HALT"))
                        {
                            invoke.state = VMState.HALT;
                        }
                        else
                        {
                            invoke.state = VMState.NONE;
                        }
                    }
                }

                callback(invoke);
            },
            ErrorHandler,
            "invokescript", new object[] { script.ByteToHex() });
        }

        public IEnumerator GetTransaction(UInt256 hash, Action<Transaction> callback)
        {
            return ExecuteRequestRPC((response) =>
            {
                if (response != null && response.HasNode("result"))
                {
                    var result = response.GetString("result");
                    var bytes = result.HexToBytes();
                    callback(Transaction.Unserialize(bytes));
                }
                else
                {
                    callback(null);
                }
            },
            ErrorHandler, "getrawtransaction", new object[] { hash.ToString() });
        }

        public IEnumerator GetBlockHeight(Action<uint> callback)
        {
            return ExecuteRequestRPC((response) =>
            {
                var blockCount = response.GetUInt32("result");
                callback(blockCount);
            },
            ErrorHandler, "getblockcount", new object[] { });
        }

        public IEnumerator GetBlock(uint height, Action<Block> callback)
        {
            return ExecuteRequestRPC((response) =>
            {
                if (response == null || !response.HasNode("result"))
                {
                    callback(null);
                    return;
                }

                var result = response.GetString("result");

                var bytes = result.HexToBytes();

                using (var stream = new MemoryStream(bytes))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        var block = Block.Unserialize(reader);
                        callback(block);
                    }
                }
            },
            ErrorHandler, "getblock", new object[] { height });
        }

        public IEnumerator GetBlock(UInt256 hash, Action<Block> callback)
        {
            return ExecuteRequestRPC((response) =>
            {
                if (response == null || !response.HasNode("result"))
                {
                    callback(null);
                    return;
                }

                var result = response.GetString("result");

                var bytes = result.HexToBytes();

                using (var stream = new MemoryStream(bytes))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        var block = Block.Unserialize(reader);
                        callback(block);
                    }
                }
            },
            ErrorHandler, "getblock", new object[] { hash.ToString() });
        }
    }

}
