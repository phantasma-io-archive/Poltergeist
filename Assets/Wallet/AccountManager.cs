using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Numerics;
using System;
using System.Linq;
using Phantasma.SDK;
using Phantasma.Neo.Core;

namespace Poltergeist
{
    public enum WalletState
    {
        Refreshing,
        Ready,
        Error
    }

    [Flags]
    public enum PlatformKind
    {
        None = 0x0,
        Phantasma = 0x1,
        Neo = 0x2,
    }

    public struct Account
    {
        public static readonly int MaxPasswordLength = 20;

        public string name;
        public PlatformKind platforms;
        public string key;
        public string password;
        public string misc;

        public override string ToString()
        {
            return $"{name.ToUpper()} [{platforms}]";
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
        public Settings Settings { get; private set; }

        public Account[] Accounts { get; private set; }

        private Dictionary<string, Token> _tokenMap = null;
        private Dictionary<string, decimal> _tokenPrices = new Dictionary<string, decimal>();
        public string CurrentTokenCurrency { get; private set; }

        private int _selectedAccountIndex;
        public Account CurrentAccount => HasSelection ? Accounts[_selectedAccountIndex] : new Account() { };

        public bool HasSelection => _selectedAccountIndex < Accounts.Length;

        private Dictionary<PlatformKind, AccountState> _states = new Dictionary<PlatformKind, AccountState>();

        public PlatformKind CurrentPlatform { get; private set; }
        public AccountState CurrentState => _states.ContainsKey(CurrentPlatform) ? _states[CurrentPlatform] : null;

        public static AccountManager Instance { get; private set; }

        public string Status { get; private set; }
        public bool Ready => Status == "ok";
        public bool Refreshing => _accountRefreshCount > 0;

        private Phantasma.SDK.PhantasmaAPI phantasmaApi;
        private Phantasma.Neo.Core.NeoAPI neoApi;

        private const string cryptoCompareAPIKey = "50f6f9f5adbb0a2f0d60145e43fe873c5a7ea1d8221b210ba14ef725f4012ee9";

        public static readonly PlatformKind[] AvailablePlatforms = new PlatformKind[] { PlatformKind.Phantasma, PlatformKind.Neo };

        private Dictionary<string, string> _currencyMap = new Dictionary<string, string>();
        public IEnumerable<string> Currencies => _currencyMap.Keys;

        private DateTime _lastPriceUpdate = DateTime.MinValue;

        private int _accountRefreshCount;

        private void Awake()
        {
            Instance = this;
            Settings = new Settings();

            Status = "Initializing wallet...";

            _currencyMap["USD"] = "$";
            _currencyMap["EUR"] = "€";
            _currencyMap["GBP"] = "£";
            _currencyMap["YEN"] = "¥";
        }

        public string GetTokenWorth(string symbol, decimal amount)
        {
            bool hasLocalCurrency = !string.IsNullOrEmpty(CurrentTokenCurrency) && _currencyMap.ContainsKey(CurrentTokenCurrency);
            if (_tokenPrices.ContainsKey(symbol) && hasLocalCurrency)
            {
                var price = _tokenPrices[symbol] * amount;
                var ch = _currencyMap[CurrentTokenCurrency];
                return $"{price} {ch}";
            }
            else
            {
                return "-";
            }
        }

        private IEnumerator FetchTokenPrice(string symbol, string currency)
        {
            var url = $"https://min-api.cryptocompare.com/data/price?fsym={symbol}&tsyms={currency}&api_key={cryptoCompareAPIKey}";
            return WebClient.RESTRequest(url, (error, msg) =>
            {

            },
            (response) =>
            {
                try
                {
                    var price = response.GetDecimal(currency);
                    SetTokenPrice(symbol, price);

                    if (symbol == "SOUL")
                    {
                        SetTokenPrice("KCAL", price / 5);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e.ToString());
                }
            });
        }

        private void SetTokenPrice(string symbol, decimal price)
        {
            Debug.Log($"Got price for {symbol} => {price}");
            _tokenPrices[symbol] = price;
        }

        // Start is called before the first frame update
        void Start()
        {
            Settings.Load();

            LoadNexus();

            var wallets = PlayerPrefs.GetString("polterwallet", "");

            if (!string.IsNullOrEmpty(wallets))
            {
                var bytes = Base16.Decode(wallets);
                Accounts = Serialization.Unserialize<Account[]>(bytes);
            }
            else
            {
                Accounts = new Account[] {
                    new Account() { name = "demo", platforms = PlatformKind.Phantasma | PlatformKind.Neo, key = "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25", password = "lol", misc = "" },
                    new Account() { name = "zion", platforms = PlatformKind.Neo, key = "KwVG94yjfVg1YKFyRxAGtug93wdRbmLnqqrFV6Yd2CiA9KZDAp4H", password = "", misc = "" },
                    new Account() { name = "master", platforms = PlatformKind.Phantasma, key = "KxDgvEKzgSBPPfuVfw67oPQBSjidEiqTHURKSDL1R7yGaGYAeYnr", password = "", misc = "" }
                };
            }
        }

        private const string TokenInfoTag = "info.tokens";

        private void PrepareTokens(Token[] tokens)
        {
            Debug.Log($"Found {tokens.Length} tokens");

            CurrentTokenCurrency = "";

            _tokenMap = new Dictionary<string, Token>();
            foreach (var token in tokens)
            {
                _tokenMap[token.symbol] = token;
            }

            Status = "ok";
        }

        public void RefreshTokenPrices()
        {
            bool needRefresh = false;

            if (CurrentTokenCurrency != Settings.currency)
            {
                needRefresh = true;
            }
            else
            {
                var diff = DateTime.UtcNow - _lastPriceUpdate;
                if (diff.TotalMinutes >= 5)
                {
                    needRefresh = true;
                }
            }
            

            if (needRefresh)
            {
                CurrentTokenCurrency = Settings.currency;
                _lastPriceUpdate = DateTime.UtcNow;

                foreach (var symbol in _tokenMap.Keys)
                {
                    if (symbol == "KCAL")
                    {
                        continue;
                    }

                    StartCoroutine(FetchTokenPrice(symbol, CurrentTokenCurrency));
                }
            }
        }

        private void LoadNexus()
        {
            phantasmaApi = new PhantasmaAPI(Settings.phantasmaRPCURL);
            neoApi = new NeoAPI(Settings.neoRPCURL, Settings.neoscanAPIURL);

            var tokenList = PlayerPrefs.GetString(TokenInfoTag, "");

            if (!string.IsNullOrEmpty(tokenList))
            {
                var tokenBytes = Base16.Decode(tokenList);
                    
                var tokens = Serialization.Unserialize<Token[]>(tokenBytes);

                PrepareTokens(tokens);
                return;
            }

            StartCoroutine(phantasmaApi.GetTokens((tokens) =>
            {
                PrepareTokens(tokens);
                var tokenBytes = Serialization.Serialize(tokens);
                PlayerPrefs.SetString(TokenInfoTag, Base16.Encode(tokenBytes));
                return;
            },
            (error, msg) =>
            {
                Status = "Failed to fetch token list...";
            }));
        }

        // Update is called once per frame
        void Update()
        {

        }

        public int GetTokenDecimals(string symbol)
        {
            if (_tokenMap.ContainsKey(symbol))
            {
                return _tokenMap[symbol].decimals;
            }

            return -1;
        }

        public decimal AmountFromString(string str, int decimals)
        {
            var n = BigInteger.Parse(str);
            return UnitConversion.ToDecimal(n, decimals);
        }

        public IEnumerator SignAndSendTransaction(string chain, byte[] script, Action<Hash> callback)
        {
            var account = this.CurrentAccount;

            switch (account.platforms)
            {
                case PlatformKind.Phantasma:
                    {
                        var keys = PhantasmaKeys.FromWIF(account.key);
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

        private Action _refreshWalletCallback;

        public List<PlatformKind> SplitFlags(PlatformKind kind)
        {
            var list = new List<PlatformKind>();
            foreach (var platform in AvailablePlatforms)
            {
                if (kind.HasFlag(platform))
                {
                    list.Add(kind);
                }
            }
            return list;
        }

        public void SelectAccount(int index)
        {
            _selectedAccountIndex = index;
            _states.Clear();
            CurrentPlatform = SplitFlags(CurrentAccount.platforms).First();
        }

        public void UnselectAcount()
        {
            _selectedAccountIndex = -1;
        }

        private void ReportWalletStatus(PlatformKind platform, AccountState state)
        {
            _accountRefreshCount--;

            if (state != null)
            {
                _states[platform] = state; 
            }

            if (_accountRefreshCount == 0)
            {
                var temp = _refreshWalletCallback;
                _refreshWalletCallback = null;
                temp?.Invoke();
            }
        }

        public void RefreshBalances(Action callback)
        {
            _refreshWalletCallback = callback;

            var platforms = SplitFlags(CurrentAccount.platforms);
            _accountRefreshCount = platforms.Count;

            var account = this.CurrentAccount;

            foreach (var platform in platforms)
            {
                switch (platform)
                {
                    case PlatformKind.Phantasma:
                        {
                            var keys = PhantasmaKeys.FromWIF(account.key);
                            StartCoroutine(phantasmaApi.GetAccount(keys.Address.Text, (x) =>
                            {
                                var state = new AccountState()
                                {
                                    address = x.address,
                                    name = x.name,
                                    stake = AmountFromString(x.stake, GetTokenDecimals("SOUL")),
                                    claim = 0, // TODO support claimable KCAL
                                    balances = x.balances.Select(y => new Balance() { Symbol = y.symbol, Amount = AmountFromString(y.amount, GetTokenDecimals(y.symbol)), Chain = y.chain, Decimals = GetTokenDecimals(y.symbol) }).ToArray(),
                                    flags = AccountFlags.None
                                };

                                if (state.stake > 50000)
                                {
                                    state.flags |= AccountFlags.Master;
                                }

                                ReportWalletStatus(platform, state);
                            },
                            (error, msg) =>
                            {
                                ReportWalletStatus(platform, null);
                            }));
                        }
                        break;

                    case PlatformKind.Neo:
                        {
                            var keys = NeoKey.FromWIF(account.key);
                            StartCoroutine(neoApi.GetAssetBalancesOf(keys, (x) =>
                            {
                                var balances = new List<Balance>();

                                foreach (var entry in x)
                                {
                                    balances.Add(new Balance()
                                    {
                                        Symbol = entry.Key,
                                        Amount = entry.Value,
                                        Chain = "main",
                                        Decimals = GetTokenDecimals(entry.Key)
                                    });
                                }

                                var state = new AccountState()
                                {
                                    address = keys.address,
                                    name = keys.address, // TODO support NNS
                                    stake = 0,
                                    claim = 0, // TODO support claimable GAS
                                    balances = balances.ToArray(),
                                    flags = AccountFlags.None
                                };

                                if (state.stake > 50000)
                                {
                                    state.flags |= AccountFlags.Master;
                                }

                                ReportWalletStatus(platform, state);
                            }));
                        }
                        break;

                    default:
                        ReportWalletStatus(platform, null);
                        break;
                }
            }
        }

        internal bool SwapSupported(string symbol)
        {
            return symbol == "SOUL";
        }
    }
}
