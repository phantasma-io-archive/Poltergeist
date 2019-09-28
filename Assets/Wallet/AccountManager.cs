using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Numerics;
using System;
using System.Linq;
using Phantasma.SDK;

namespace Poltergeist
{    
    public struct Account
    {
        public static readonly int MaxPasswordLength = 20;

        public string name;
        public string platform;
        public string key;
        public string password;
        public string misc;

        public override string ToString()
        {
            return $"{name.ToUpper()} [{platform}]";
        }
    }

    public enum AccountFlags
    {
        None = 0x0,
        Master = 0x1,
        Validator = 0x2
    }

    public class AccountState
    {
        public string name;
        public string address;
        public decimal stake;
        public decimal claim;
        public Balance[] balances;
        public AccountFlags flags;
    }

    public struct Balance
    {
        public string Symbol;
        public decimal Amount;
        public string Chain;
        public int Decimals;
    }

    public class AccountManager : MonoBehaviour
    {
        public const string neoscan_url = "http://mankinighost.phantasma.io:4000";
        public const string neo_rpc = "http://mankinighost.phantasma.io:30333";
        public const string phantasma_rpc_url = "http://localhost:7077/rpc";

        public Account[] Accounts { get; private set; }

        private Dictionary<string, Token> _tokens = null;

        public static AccountManager Instance { get; private set; }

        public bool Ready { get; private set; }

        private Phantasma.SDK.API phantasmaApi;

        private void Awake()
        {
            Instance = this;
            Ready = false;
        }

        // Start is called before the first frame update
        void Start()
        {
            phantasmaApi = new Phantasma.SDK.API(phantasma_rpc_url);

            StartCoroutine(phantasmaApi.GetTokens((tokens) =>
            {
                Debug.Log($"Got {tokens.Length} tokens from api");
                _tokens = new Dictionary<string, Token>();
                foreach (var token in tokens)
                {
                    _tokens[token.symbol] = token;
                }

                Ready = true;
            }));

            var wallets = PlayerPrefs.GetString("polterwallet", "");

            if (!string.IsNullOrEmpty(wallets))
            {
                var bytes = Base16.Decode(wallets);
                Accounts = Serialization.Unserialize<Account[]>(bytes);
            }
            else
            {
                Accounts = new Account[] { new Account() { name = "demo", platform = "phantasma", key = "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25", password = "lol", misc = "" } };
            }
        }

        // Update is called once per frame
        void Update()
        {

        }

        public int GetTokenDecimals(string symbol)
        {
            if (_tokens.ContainsKey(symbol))
            {
                return _tokens[symbol].decimals;
            }

            return -1;
        }

        public decimal AmountFromString(string str, int decimals)
        {
            var n = BigInteger.Parse(str);
            return UnitConversion.ToDecimal(n, decimals);
        }

        public IEnumerator SignAndSendTransaction(Account account, string chain, byte[] script, Action<Hash> callback)
        {
            switch (account.platform)
            {
                case "phantasma":
                    {
                        var keys = KeyPair.FromWIF(account.key);
                        return phantasmaApi.SignAndSendTransaction(keys, script, chain, (hashText) =>
                        {
                            var hash = Hash.Parse(hashText);
                            callback(hash);
                        }, (error, msg) =>
                        {
                            callback(Hash.Null);
                        });
                    }

                default:
                    callback(Hash.Null);
                    return null;
            }
        }

        public IEnumerator FetchBalances(Account account, Action<AccountState> callback)
        {
            switch (account.platform)
            {
                case "phantasma":
                    {
                        var keys = KeyPair.FromWIF(account.key);
                        return phantasmaApi.GetAccount(keys.Address.Text, (x) =>
                        {
                            var state = new AccountState()
                            {
                                address = x.address,
                                name = x.name,
                                stake = AmountFromString(x.stake, GetTokenDecimals("SOUL")),
                                claim = 0,
                                balances = x.balances.Select(y => new Balance() { Symbol = y.symbol, Amount = AmountFromString(y.amount, GetTokenDecimals(y.symbol)), Chain = y.chain, Decimals = GetTokenDecimals(y.symbol) }).ToArray(),
                                flags = AccountFlags.None
                            };

                            if (state.stake > 50000)
                            {
                                state.flags |= AccountFlags.Master;
                            }

                            callback(state);
                        },
                        (error, msg) =>
                        {
                            callback(null);
                        });
                    }

                default:
                    callback(null);
                    return null;
            }
        }

        internal bool SwapSupported(string symbol)
        {
            return symbol == "SOUL";
        }
    }
}
