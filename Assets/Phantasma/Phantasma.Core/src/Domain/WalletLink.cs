using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LunarLabs.Parser;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Numerics;

namespace Phantasma.Core.Domain
{
    public enum WalletStatus
    {
        Closed,
        Ready
    }
    
    public abstract class WalletLink
    {
        public const int WebSocketPort = 7090;
        public const int LinkProtocol = 3;

        public struct Error : IAPIResult
        {
            public string message;
        }

        public struct Authorization : IAPIResult
        {
            public string wallet;
            public string nexus;
            public string dapp;
            public string token;
            public int version;
        }

        public struct Balance : IAPIResult
        {
            public string symbol;
            public string value;
            public int decimals;
            public string[] ids;
        }

        public struct File: IAPIResult
        {
            public string name;
            public int size;
            public uint date;
            public string hash;
        }

        public struct Account : IAPIResult
        {
            public string alias;
            public string address;
            public string name;
            public string avatar;
            public string platform;
            public string external;
            public Balance[] balances;
            public File[] files;
        }

        public struct Invocation : IAPIResult
        {
            public string result; // deprecated
            public string[] results;
        }

        public struct Transaction : IAPIResult
        {
            public string hash;
        }
        
        public struct MultiSig : IAPIResult
        {
            public bool success;
            public string message;
        }

        public struct Signature : IAPIResult
        {
            public string signature;
            public string random;
        }
        
        public struct Peer : IAPIResult
        {
            public string peer;
        }
        
        public struct NexusResult : IAPIResult
        {
            public string nexus;
        }
        
        public struct WalletVersion : IAPIResult
        {
            public string version;
        }

        public class Connection
        {
            public readonly string Token;
            public readonly int Version;

            public Connection(string token, int version)
            {
                this.Token = token;
                this.Version = version;
            }
        }

        private Random rnd = new Random();

        private Dictionary<string, Connection> _connections = new Dictionary<string, Connection>();

        protected abstract WalletStatus Status { get; }

        public abstract string Nexus { get; }

        public abstract string Name { get; }

        private bool _isPendingRequest;

        public WalletLink()
        {
        }

        private Connection ValidateRequest(string[] args)
        {
            if (args.Length >= 3)
            {
                string dapp = args[args.Length - 2];
                string token = args[args.Length - 1];

                if (_connections.ContainsKey(dapp))
                {
                    var connection = _connections[dapp];
                    if (connection.Token == token)
                    {
                        return connection;
                    }
                }
            }

            return null;
        }

        protected abstract void Authorize(string dapp, string token, int version, Action<bool, string> callback);

        protected abstract void GetAccount(string platform, int version, Action<Account, string> callback);

        protected abstract void GetPeer(Action<string> callback);

        protected abstract void GetNexus(Action<string> callback);
        
        protected abstract void GetWalletVersion(Action<string> callback);
        
        protected abstract void InvokeScript(string chain, byte[] script, int id, Action<string[], string> callback);

        // NOTE for security, signData should not be usable as a way of signing transaction. That way the wallet is responsible for appending random bytes to the message, and return those in callback
        protected abstract void SignData(string platform, SignatureKind kind, byte[] data, int id, Action<string, string, string> callback);

        protected abstract void SignTransactionSignature(Phantasma.Core.Domain.Transaction transaction, string platform,
            SignatureKind kind, Action<Phantasma.Core.Cryptography.Signature, string> callback);
        
        protected abstract void FetchAndMultiSignature(string subject, string platform,
            SignatureKind kind, int id, Action<bool, string> callback);
        
        protected abstract void SignTransaction(string platform, SignatureKind kind, string chain, byte[] script, byte[] payload, int id, ProofOfWork pow, Action<Hash, string> callback);

        protected abstract void WriteArchive(Hash hash, int blockIndex, byte[] data, Action<bool, string> callback);
        
        #region Authorization
        private void HandleAuthorize(string[] args, Connection connection, int id, Action<int, DataNode, bool> callback)
        {
            DataNode answer;
            bool success = false;
            
            if ( args.Length < 1 || args.Length > 2)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = "authorize: Invalid number of arguments" });
                callback(id, answer, false);
                _isPendingRequest = false;
                return;
            }
            
            string token;
            var dapp = args[0];

            int version = 1;
            
            if (args.Length == 2)
            {
                var str = args[1];
                if (!int.TryParse(str, out version))
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = $"authorize: Invalid version: {str}"});
                    callback(id, answer, false);
                    _isPendingRequest = false;
                    return;
                }
            }

            if (_connections.ContainsKey(dapp))
            {
                connection = _connections[dapp];
                success = true;
                answer = APIUtils.FromAPIResult(new Authorization()
                {
                    wallet = this.Name, nexus = this.Nexus, dapp = dapp, token = connection.Token,
                    version = connection.Version
                });
                callback(id, answer, success);
                _isPendingRequest = false;
                return;
            }

            var bytes = new byte[32];
            rnd.NextBytes(bytes);
            token = Base16.Encode(bytes);

            this.Authorize(dapp, token, version, (authorized, error) =>
            {
                if (authorized)
                {
                    _connections[dapp] = new Connection(token, version);

                    success = true;
                    answer = APIUtils.FromAPIResult(new Authorization() { wallet = this.Name, nexus = this.Nexus, dapp = dapp, token = token });
                }
                else
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = error});
                }

                callback(id, answer, success);
                _isPendingRequest = false;
            });
        }
        #endregion
        
        #region Account
        private void HandleGetAccount(string[] args, Connection connection, int id, Action<int, DataNode, bool> callback)
        {
            int expectedLength;
            DataNode answer;
            bool success = false;

            switch (connection.Version)
            {
                case 1:
                    expectedLength = 0;
                    break;

                default:
                    expectedLength = 1;
                    break;
            }

            if (args.Length != expectedLength)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"getAccount: Invalid amount of arguments: {args.Length}" });
                callback(id, answer, success);
                _isPendingRequest = false;
                return;
            }
            
            string platform;

            if (connection.Version >= 2)
            {
                platform = args[0].ToLower();
            }
            else
            {
                platform = "phantasma";
            }

            GetAccount(platform, connection.Version, (account, error) => {
                if (error == null)
                {
                    success = true;
                    answer = APIUtils.FromAPIResult(account);
                }
                else
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = error });
                }

                callback(id, answer, success);
                _isPendingRequest = false;
            });
        }
        #endregion
        
        #region Nexus

        private void HandleGetNexus(string[] args, Connection connection, int id, Action<int, DataNode, bool> callback)
        {
            DataNode answer;
            bool success = false;

            if (args.Length > 1)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"getNexus: Invalid amount of arguments: {args.Length}" });
                callback(id, answer, success);
                _isPendingRequest = false;
                return;
            }

            GetNexus((nexus) => {
                success = true;
                answer = APIUtils.FromAPIResult(new NexusResult { nexus = nexus });
                callback(id, answer, success);
                _isPendingRequest = false;
            });
        }
        #endregion
        
        #region Peer
        private void HandleGetPeer(string[] args, Connection connection, int id, Action<int, DataNode, bool> callback)
        {
            DataNode answer;
            bool success = false;

            if (args.Length > 1)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"getPeer: Invalid amount of arguments: {args.Length}" });
                callback(id, answer, success);
                _isPendingRequest = false;
                return;
            }

            GetPeer((peer) => {
                success = true;
                answer = APIUtils.FromAPIResult(new Peer() { peer = peer });
                callback(id, answer, success);
                _isPendingRequest = false;
            });
        }
        #endregion

        #region Wallet Version
        private void HandleGetWalletVersion(string[] args, Connection connection, int id, Action<int, DataNode, bool> callback)
        {
            DataNode answer;
            bool success = false;

            if (args.Length > 1)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"getWalletVersion: Invalid amount of arguments: {args.Length}" });
                callback(id, answer, success);
                _isPendingRequest = false;
                return;
            }

            GetWalletVersion((version) => {
                success = true;
                answer = APIUtils.FromAPIResult(new WalletVersion() { version = version });
                callback(id, answer, success);
                _isPendingRequest = false;
            });
        }
        #endregion
        
        #region Sign Data

        private void HandleSignData(string[] args, Connection connection, int id, Action<int, DataNode, bool> callback)
        {
            int expectedLength;
            DataNode answer;
            bool success = false;

            switch (connection.Version)
            {
                case 1:
                    expectedLength = 2;
                    break;

                default:
                    expectedLength = 3;
                    break;
            }

            if (args.Length != expectedLength)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"signData: Invalid amount of arguments: {args.Length}" });
                callback(id, answer, success);
                _isPendingRequest = false;
                return;
            }

            var data = Base16.Decode(args[0], false);
            if (data == null)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"signData: Invalid input received" });
                callback(id, answer, success);
                _isPendingRequest = false;
                return;
            }
            
            SignatureKind signatureKind;

            if (!Enum.TryParse<SignatureKind>(args[1], out signatureKind))
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"signData: Invalid signature: " + args[1] });
                callback(id, answer, false);
                _isPendingRequest = false;
                return;
            }

            var platform = connection.Version >= 2 ? args[2].ToLower() : "phantasma";

            SignData(platform, signatureKind, data, id, (signature, random, txError) => {
                if (signature != null)
                {
                    success = true;
                    answer = APIUtils.FromAPIResult(new Signature() { signature = signature, random = random });
                }
                else
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = txError });
                }

                callback(id, answer, success);
                _isPendingRequest = false;
            });
        }

        #endregion
        
        #region Sign Tx

        private void HandleSignTx(string[] args, Connection connection, int id, Action<int, DataNode, bool> callback)
        {
            int index = 0;
            DataNode answer;
            bool success = false;

            if (connection.Version == 1)
            {
                if (args.Length != 4)
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid amount of arguments: {args.Length}" });
                    callback(id, answer, false);
                    _isPendingRequest = false;
                    return;
                }
                
                var txNexus = args[index]; index++;
                if (txNexus != this.Nexus)
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Expected nexus {this.Nexus}, instead got {txNexus}" });
                    callback(id, answer, false);
                    _isPendingRequest = false;
                    return;
                }
            }
            else if (connection.Version == 2)
            {
                if (args.Length != 5 && args.Length != 6)
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid amount of arguments: {args.Length}" });
                    callback(id, answer, false);
                    _isPendingRequest = false;
                    return;
                }
            }

            var chain = args[index]; index++;
            var script = Base16.Decode(args[index], false); index++;

            if (script == null)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid script data" });
                callback(id, answer, false);
                _isPendingRequest = false;
                return;
            }
            
            byte[] payload = args[index].Length > 0 ? Base16.Decode(args[index], false) : null;
            index++;

            string platform;
            SignatureKind signatureKind;

            if (connection.Version >= 2) {
                if (!Enum.TryParse<SignatureKind>(args[index], out signatureKind))
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid signature: " + args[index] });
                    callback(id, answer, false);
                    _isPendingRequest = false;
                    return;
                }
                index++;

                platform = args[index].ToLower();
                index++;
            }
            else 
            {
                platform = "phantasma";
                signatureKind = SignatureKind.Ed25519;
            }

            var pow = ProofOfWork.None;
            if (args.Length == 6) // Optional argument
            {
                if (!Enum.TryParse<ProofOfWork>(args[5], out pow))
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid POW argument: " + args[index] });
                    callback(id, answer, false);
                    _isPendingRequest = false;
                    return;
                }
            }

            SignTransaction(platform, signatureKind, chain, script, payload, id, pow, (hash, txError) => {
                if (hash != Hash.Null)
                {
                    success = true;
                    answer = APIUtils.FromAPIResult(new Transaction() { hash = hash.ToString() });
                }
                else
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = txError });
                }

                callback(id, answer, success);
                _isPendingRequest = false;
            });
            
        }
        
        #endregion
        
        #region SignTxSignature

        private void HandleSignTxSignature(string[] args, Connection connection, int id,
            Action<int, DataNode, bool> callback)
        {
            int expectedLength;
            DataNode answer;
            bool success = false;

            switch (connection.Version)
            {
                case 1:
                    expectedLength = 2;
                    break;

                default:
                    expectedLength = 3;
                    break;
            }

            if (args.Length != expectedLength)
            {
                answer = APIUtils.FromAPIResult(new Error()
                    { message = $"signData: Invalid amount of arguments: {args.Length}" });
                callback(id, answer, false);
                _isPendingRequest = false;
                return;
            }

            var data = Base16.Decode(args[0], false);
            if (data == null)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"signData: Invalid input received" });
                callback(id, answer, false);
                _isPendingRequest = false;
                return;
            }
            
            SignatureKind signatureKind;

            if (!Enum.TryParse<SignatureKind>(args[1], out signatureKind))
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"signData: Invalid signature: " + args[1] });
                callback(id, answer, false);
                _isPendingRequest = false;
                return;
            }

            var platform = connection.Version >= 2 ? args[2].ToLower() : "phantasma";

            var transaction = Phantasma.Core.Domain.Transaction.Unserialize(data);
            
            SignTransactionSignature(transaction, platform, signatureKind, (signature, txError) => {
                if (signature != null)
                {
                    success = true;
                    answer = APIUtils.FromAPIResult(new Signature() { signature = Base16.Encode(signature.ToByteArray()) });
                }
                else
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = txError });
                }

                callback(id, answer, success);
                _isPendingRequest = false;
            });
            
        }
        #endregion
        
        #region Multi Sig

        private void HandleMultiSig(string[] args, Connection connection, int id, Action<int, DataNode, bool> callback)
        {
            int expectedLength;
            DataNode answer;
            bool success = false;

            switch (connection.Version)
            {
                case 1:
                    expectedLength = 2;
                    break;

                default:
                    expectedLength = 3;
                    break;
            }

            if (args.Length != expectedLength)
            {
                answer = APIUtils.FromAPIResult(new Error()
                    { message = $"signData: Invalid amount of arguments: {args.Length}" });
                callback(id, answer, false);
                _isPendingRequest = false;
                return;
            }

            /*var subjectBytes = Base16.Decode(args[0], false);
            if (subjectBytes == null)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"signData: Invalid input received" });
                callback(id, answer, false);
                _isPendingRequest = false;
                return;
            }*/
            
            var subject = args[0];

            SignatureKind signatureKind;
            if (!Enum.TryParse<SignatureKind>(args[1], out signatureKind))
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"signData: Invalid signature: " + args[1] });
                callback(id, answer, false);
                _isPendingRequest = false;
                return;
            }

            var platform = connection.Version >= 2 ? args[2].ToLower() : "phantasma";

            FetchAndMultiSignature(subject, platform, signatureKind, id, (_success, txError) => {
                if (_success)
                {
                    success = true;
                    answer = APIUtils.FromAPIResult(new MultiSig{ message = txError, success = _success});
                }
                else
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = txError });
                }

                callback(id, answer, success);
                _isPendingRequest = false;
            });
        }
        #endregion
        
        #region Invoke Raw Script
        private void HandleInvokeRawScript(string[] args, Connection connection, int id, Action<int, DataNode, bool> callback)
        {
            bool success = false;
            DataNode answer;
            if (args.Length != 2)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"invokeScript: Invalid amount of arguments: {args.Length}"});
                callback(id, answer, success);
                _isPendingRequest = false;
                return;
            }

            var chain = args[0];
            var script = Base16.Decode(args[1], false);

            if (script == null)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"invokeScript: Invalid script data" });
                callback(id, answer, success);
                _isPendingRequest = false;
                return;
            }
            
            InvokeScript(chain, script, id, (invokeResults, invokeError) =>
            {
                if (invokeResults != null && invokeResults.Length > 0)
                {
                    success = true;
                    answer = APIUtils.FromAPIResult(new Invocation() { result = invokeResults[0], results = invokeResults });
                }
                else
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = invokeError });
                }

                callback(id, answer, success);
                _isPendingRequest = false;
            });
        }
        #endregion
        
        #region Write Archive

        private void HandleWriteArchive(string[] args, Connection connection, int id,
            Action<int, DataNode, bool> callback)
        {
            DataNode answer;
            bool success = false;
            
            if (args.Length != 3)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"writeArchive: Invalid amount of arguments: {args.Length}" });
                callback(id, answer, success);
                _isPendingRequest = false;
                return;
            }
            
            var archiveHash = Hash.Parse(args[0]);
            var blockIndex = int.Parse(args[1]);
            var bytes = Base16.Decode(args[2], false);

            if (bytes == null)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"invokeScript: Invalid archive data"});
                callback(id, answer, false);
                _isPendingRequest = false;
                return;
            }
            
            WriteArchive(archiveHash, blockIndex, bytes, (result, error) =>
            {
                if (result)
                {
                    success = true;
                    answer = APIUtils.FromAPIResult(new Transaction() { hash = archiveHash.ToString() });
                }
                else
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = error });
                }

                callback(id, answer, success);
                _isPendingRequest = false;
            });
        }
        #endregion
        
        public void Execute(string cmd, Action<int, DataNode, bool> callback)
        {
            var args = cmd.Split(',');

            DataNode answer;

            int id = 0;

            if (!int.TryParse(args[0], out id))
            {
                answer = APIUtils.FromAPIResult(new Error() { message = "Invalid request id" });
                callback(id, answer, false);
                return;
            }

            if (args.Length != 2)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = "Malformed request" });
                callback(id, answer, false);
                return;
            }

            cmd = args[1];
            args = cmd.Split('/');

            bool success = false;

            var requestType = args[0];

            if (requestType != "authorize")
            {
                var status = this.Status;
                if (status != WalletStatus.Ready)
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = $"Wallet is {status}" });
                    callback(id, answer, false);
                    return;
                }
            }

            if (_isPendingRequest)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"A previous request is still pending" });
                callback(id, answer, false);
                return;
            }

            _isPendingRequest = true;

            Connection connection = null;

            if (requestType != "authorize")
            {
                connection = ValidateRequest(args);
                if (connection == null)
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = "Invalid or missing API token" });
                    callback(id, answer, false);
                    return;
                }

                // exclude dapp/token args
                args = args.Take(args.Length - 2).ToArray();
            }

            args = args.Skip(1).ToArray();

            switch (requestType)
            {
                case "authorize":
                {
                    HandleAuthorize(args, connection, id, callback);
                    return;
                }
                
                case "getAccount":
                {
                    HandleGetAccount(args, connection, id, callback);
                    return;
                }

                case "getPeer":
                {
                    HandleGetPeer(args, connection, id, callback);
                    return;
                }
                
                case "getWalletVersion":
                {
                    HandleGetWalletVersion(args, connection, id, callback);
                    return;
                }

                case "signData":
                {
                    HandleSignData(args, connection, id, callback);
                    return;
                }

                case "signTx":
                {
                    HandleSignTx(args, connection, id, callback);
                    return;
                }
                
                case "signTxSignature":
                {
                    HandleSignTxSignature(args, connection, id, callback);
                    return;
                }

                case "multiSig":
                {
                    HandleMultiSig(args, connection, id, callback);
                    return;
                }
                
                case "invokeScript":
                {
                    HandleInvokeRawScript(args, connection, id, callback);
                    return;
                }

                case "getNexus":
                {
                    HandleGetNexus(args, connection, id, callback);
                    return;
                }

                case "writeArchive":
                {
                    HandleWriteArchive(args, connection, id, callback);
                    return;
                }

                default:
                    answer = APIUtils.FromAPIResult(new Error() { message = "Invalid request type" });
                    break;
            }

            callback(id, answer, success);
            _isPendingRequest = false;
        }

        public void Revoke(string dapp, string token)
        {
            Throw.If(!_connections.ContainsKey(dapp), "unknown dapp");

            var connection = _connections[dapp];
            Throw.If(connection.Token != token, "invalid token");

            _connections.Remove(dapp);
        }
    }
}
