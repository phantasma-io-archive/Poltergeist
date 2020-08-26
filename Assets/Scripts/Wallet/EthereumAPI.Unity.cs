using System;
using System.Collections;
using LunarLabs.Parser;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Ethereum;
using Phantasma.Ethereum.Signer;

namespace Phantasma.SDK
{
    public class EthereumAPI
    {
        public readonly string Host;

        public EthereumAPI(string host)
        {
            this.Host = host;
        }
        public IEnumerator GetBalance(string addressText, string tokenSymbol, int tokenDecimals, Action<Poltergeist.Balance> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null)
        {
            yield return WebClient.RPCRequest(Host, "eth_getBalance", errorHandlingCallback, (node) => {
                var availableHex = node.Value;
                var available = BigInteger.FromHex(availableHex.Substring(2));

                var balance = new Poltergeist.Balance()
                {
                    Symbol = tokenSymbol,
                    Available = UnitConversion.ToDecimal(available, tokenDecimals),
                    Pending = 0,
                    Claimable = 0,
                    Staked = 0,
                    Chain = "main",
                    Decimals = tokenDecimals
                };

                callback(balance);
            }, addressText, "latest");
        }
        //Returns the balance for a specific token, given a contract.
        public IEnumerator GetTokenBalance(string addressText, string tokenContract, string tokenSymbol, int tokenDecimals, Action<Poltergeist.Balance> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null)
        {
            var balanceOf = "70a08231b98ef4ca268c9cc3f6b4590e4bfec28280db06bb5d45e689f2a360be";
            var data = balanceOf.Substring(0, 8) + addressText.Substring(2).PadLeft(64, '0');

            var paramData = DataNode.CreateArray("params");
            var callParams = DataNode.CreateObject();
            callParams.AddField("to", "0x" + tokenContract);
            callParams.AddField("data", data);
            paramData.AddNode(callParams);
            paramData.AddField(null, "latest");

            yield return WebClient.RPCRequestEx(Host, "eth_call", errorHandlingCallback, (node) => {
                var availableHex = node.Value;
                BigInteger available = 0;
                if (!String.IsNullOrEmpty(availableHex) && availableHex != "0x")
                    available = BigInteger.FromHex(availableHex.Substring(2));

                var balance = new Poltergeist.Balance()
                {
                    Symbol = tokenSymbol,
                    Available = UnitConversion.ToDecimal(available, tokenDecimals),
                    Pending = 0,
                    Claimable = 0,
                    Staked = 0,
                    Chain = "main",
                    Decimals = tokenDecimals
                };

                callback(balance);
            }, paramData);
        }
        public IEnumerator GetNonce(string addressText, Action<Int32> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null)
        {
            yield return WebClient.RPCRequest(Host, "eth_getTransactionCount", errorHandlingCallback, (node) => {
                var hex = node.Value;
                if (string.IsNullOrEmpty(hex))
                {
                    throw new Exception("Error: Cannot get nounce!");
                }

                var nonce = Convert.ToInt32(hex, 16);

                callback(nonce);
            }, addressText, "latest");
        }
        public string SignTransaction(EthereumKey keys, int nonce, string receiveAddress, BigInteger amount, BigInteger gasPrice, BigInteger gasLimit, string data = null)
        {
            var realAmount = System.Numerics.BigInteger.Parse(amount.ToString());

            //Create a transaction from scratch
            var tx = new Phantasma.Ethereum.Signer.Transaction(receiveAddress, realAmount, nonce, 
                System.Numerics.BigInteger.Parse(gasPrice.ToString()),
                System.Numerics.BigInteger.Parse(gasLimit.ToString()),
                data);

            tx.Sign(new EthECKey(keys.PrivateKey, true));

            var encoded = tx.GetRLPEncoded();

            return "0x" + Base16.Encode(encoded);
        }
        public string SignTokenTransaction(EthereumKey keys, int nonce, string tokenContract, string receiveAddress, BigInteger amount, BigInteger gasPrice, BigInteger gasLimit)
        {
            var transferMethodHash = "a9059cbb";
            var to = receiveAddress.Substring(2).PadLeft(64, '0');
            var amountHex = amount.ToHex().PadLeft(64, '0');

            //Create a transaction from scratch
            var tx = new Phantasma.Ethereum.Signer.Transaction(tokenContract,
                0, // Ammount of ETH to be transfered (0)
                nonce,
                System.Numerics.BigInteger.Parse(gasPrice.ToString()),
                System.Numerics.BigInteger.Parse(gasLimit.ToString()),
                transferMethodHash + to + amountHex);

            tx.Sign(new EthECKey(keys.PrivateKey, true));

            var encoded = tx.GetRLPEncoded();

            return "0x" + Base16.Encode(encoded);
        }
        public IEnumerator SendRawTransaction(string hexTx, Action<Hash, string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null)
        {
            yield return WebClient.RPCRequest(Host, "eth_sendRawTransaction", errorHandlingCallback, (node) => {
                var hash = Hash.Parse(node.Value);
                callback(hash, null);
            }, hexTx);
        }
        public IEnumerator GetTransactionByHash(string hash, Action<DataNode> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null)
        {
            yield return WebClient.RPCRequest(Host, "eth_getTransactionByHash", errorHandlingCallback, callback, "0x" + hash);
        }
    }
}