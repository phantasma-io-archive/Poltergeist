using LunarLabs.Parser;
using System;
using System.Linq;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using Poltergeist.PhantasmaLegacy.Cryptography;
using Poltergeist.Neo2.Utils;
using Poltergeist.Neo2.VM.Types;
using Phantasma.SDK;

namespace Poltergeist.Neo2.Core
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

        public static byte[] GetScriptHashFromString(string hash)
        {
            hash = hash.ToLower();
            if (hash.StartsWith("0x"))
            {
                hash = hash.Substring(2);
            }

            return hash.HexToBytes().Reverse().ToArray();
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

        public void GenerateInputsOutputs(Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, UnspentEntries unspent, NeoKeys key, string symbol, IEnumerable<Transaction.Output> targets, out List<Transaction.Input> inputs, out List<Transaction.Output> outputs, decimal system_fee = 0, bool allowSameSourceAndDest = false)
        {
            var from_script_hash = new UInt160(key.signatureHash.ToArray());
            var info = GetAssetsInfo();
            var targetAssetID = NeoUtils.ReverseHex(info[symbol]).HexToBytes();
            if (targets != null)
                foreach (var t in targets)
                    if (t.assetID == null)
                        t.assetID = targetAssetID;
            //else Console.WriteLine("ASSETID target already existed: " + symbol);
            GenerateInputsOutputs(errorHandlingCallback, unspent, from_script_hash, targets, out inputs, out outputs, system_fee, allowSameSourceAndDest);
        }

        public void GenerateInputsOutputs(Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, UnspentEntries unspent, UInt160 from_script_hash, IEnumerable<Transaction.Output> targets, out List<Transaction.Input> inputs, out List<Transaction.Output> outputs, decimal system_fee = 0, bool allowSameSourceAndDest = false)
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
                {
                    errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, $"Not enough {assetName} in address {from_address}");
                    return;
                }

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

                    if (selected < system_fee)
                    {
                        errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, $"Not enough {assetName} in address {from_address}");
                        return;
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

            if (!allowSameSourceAndDest)
            {
                foreach (var target in targets)
                {
                    if (target.scriptHash.Equals(from_script_hash))
                    {
                        errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, "Target can't be same as input");
                        return;
                    }
                }
            }

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
                if (system_fee > 0 && assetName == "GAS")
                {
                    done_fee = true;
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
                {
                    errorHandlingCallback(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, $"Not enough {assetName} in address {from_address}");
                    return;
                }

                if (cost > 0)
                    foreach (var target in thistargets)
                        outputs.Add(target);

                if (selected > cost)
                    outputs.Add(new Transaction.Output()
                    {
                        assetID = targetAssetID,
                        scriptHash = from_script_hash,
                        value = selected - cost
                    });
            }
        }

        public IEnumerator CallContract(Action<Transaction, string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, UnspentEntries unspent, NeoKeys key, UInt160 scriptHash, string operation, object[] args, string interop, decimal fee = 0, string attachSymbol = null, IEnumerable<Transaction.Output> attachTargets = null)
        {
            return CallContract(callback, errorHandlingCallback, unspent, key, scriptHash, new object[] { operation, args }, interop, fee, attachSymbol, attachTargets);
        }

        public IEnumerator CallContract(Action<Transaction, string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, UnspentEntries unspent, NeoKeys key, UInt160 scriptHash, object[] args, string interop, decimal fee = 0, string attachSymbol = null, IEnumerable<Transaction.Output> attachTargets = null)
        {
            var bytes = GenerateScript(scriptHash, args);
            return CallContract(callback, errorHandlingCallback, unspent, key, scriptHash, bytes, interop, fee, attachSymbol, attachTargets);
        }

        public IEnumerator CallContract(Action<Transaction, string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, UnspentEntries unspent, NeoKeys key, UInt160 scriptHash, byte[] bytes, string interop, decimal fee = 0, string attachSymbol = null, IEnumerable<Transaction.Output> attachTargets = null)
        {
            List<Transaction.Input> inputs = null;
            List<Transaction.Output> outputs = null;

            if (attachSymbol == null)
            {
                attachSymbol = "GAS";
            }   
            
            if (!string.IsNullOrEmpty(attachSymbol))
            {
                GenerateInputsOutputs(errorHandlingCallback, unspent, key, attachSymbol, attachTargets, out inputs, out outputs, fee);

                if (inputs.Count == 0)
                {
                    throw new Exception($"Not enough inputs for transaction");
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
            }, errorHandlingCallback);
        }

        public IEnumerator SendAsset(Action<Transaction, string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, UnspentEntries unspent, NeoKeys fromKey, string toAddress, string symbol, decimal amount, string interop, decimal fee = 0, bool allowSameSourceAndDest = false)
        {
            if (!allowSameSourceAndDest && String.Equals(fromKey.Address, toAddress, StringComparison.OrdinalIgnoreCase))
            {
                throw new NeoException("Source and dest addresses are the same");
            }

            var toScriptHash = toAddress.GetScriptHashFromAddress();
            var target = new Transaction.Output() { scriptHash = new UInt160(toScriptHash), value = amount };
            var targets = new List<Transaction.Output>() { target };
            return SendAsset(callback, errorHandlingCallback, unspent, fromKey, symbol, targets, interop, fee, allowSameSourceAndDest);
        }

        public IEnumerator SendAsset(Action<Transaction, string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, UnspentEntries unspent, NeoKeys fromKey, string symbol, IEnumerable<Transaction.Output> targets, string interop, decimal fee = 0, bool allowSameSourceAndDest = false)
        {
            List<Transaction.Input> inputs;
            List<Transaction.Output> outputs;

            GenerateInputsOutputs(errorHandlingCallback, unspent, fromKey, symbol, targets, out inputs, out outputs, fee, allowSameSourceAndDest);

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
            }, errorHandlingCallback);
        }

        public IEnumerator ClaimGas(UnspentEntries unspent, NeoKeys ownerKey, List<UnspentEntry> claimable, decimal amount, Action<Transaction, string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback)
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
            }, errorHandlingCallback);
        }

        public IEnumerator GetUnspent(string address, Action<UnspentEntries> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback)
        {
            return GetUnspent(new UInt160(address.AddressToScriptHash()), callback, errorHandlingCallback);
        }

        private void LogData(DataNode node, int ident = 0)
        {
            var tabs = new string('\t', ident);
            Logger($"{tabs}{node}");
            foreach (DataNode child in node.Children)
                LogData(child, ident + 1);
        }

        public IEnumerator ExecuteRequestRPC(Action<DataNode> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> onError, string method, object[] _params)
        {
            return WebClient.RPCRequest(this.neoRPCUrl, method, WebClient.NoTimeout, 0, onError, (response) => callback(response), _params);
        }

        // Note: This current implementation requires NeoScan running at port 4000
        public IEnumerator GetUnspent(UInt160 hash, Action<UnspentEntries> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback)
        {
            var url = this.neoscanUrl + "/api/main_net/v1/get_balance/" + hash.ToAddress();

            return WebClient.RESTRequest(url, WebClient.NoTimeout,
                errorHandlingCallback,
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
                });

        }

        // Note: This current implementation requires NeoScan running at port 4000
        public IEnumerator GetClaimable(string address, Action<List<UnspentEntry>, decimal> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback)
        {
            var url = this.neoscanUrl + "/api/main_net/v1/get_claimable/" + address;
            return WebClient.RESTRequest(url, WebClient.NoTimeout,
                errorHandlingCallback,
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
                });
        }

        public IEnumerator GetUnclaimed(string address, Action<decimal> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback)
        {
            var url = this.neoscanUrl + "/api/main_net/v1/get_unclaimed/" + address;
            return WebClient.RESTRequest(url, WebClient.NoTimeout,
                errorHandlingCallback,
                (response) =>
                {
                    var amount = response.GetDecimal("unclaimed");

                    callback(amount);
                });
        }

        public IEnumerator SendRawTransaction(string hexTx, Action<string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback)
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
            errorHandlingCallback, "sendrawtransaction", new object[] { hexTx });
        }

        protected IEnumerator SendTransaction(Transaction tx, Action<string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback)
        {
            var rawTx = tx.Serialize(true);
            var hexTx = rawTx.ByteToHex();

            return SendRawTransaction(hexTx, callback, errorHandlingCallback);
        }
    }

}
