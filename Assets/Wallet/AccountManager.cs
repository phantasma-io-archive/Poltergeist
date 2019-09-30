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

    public struct HistoryEntry
    {
        public string hash;
        public DateTime date;
        public string url;
    }

    public enum AccountFlags
    {
        None = 0x0,
        Master = 0x1,
        Validator = 0x2
    }

    public static class AccountFlagsExtensions
    {
        public static List<PlatformKind> Split(this PlatformKind kind)
        {
            var list = new List<PlatformKind>();
            foreach (var platform in AccountManager.AvailablePlatforms)
            {
                if (kind.HasFlag(platform))
                {
                    list.Add(platform);
                }
            }
            return list;
        }
    }

    public class AccountState
    {
        public string name;
        public string address;
        public decimal stake;
        public decimal claim;
        public Balance[] balances;
        public AccountFlags flags;

        public decimal GetBalance(string symbol)
        {
            for (int i=0; i<balances.Length; i++)
            {
                var entry = balances[i];
                if (entry.Symbol == symbol)
                {
                    return entry.Amount;
                }
            }

            return 0;
        }
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
        private Dictionary<PlatformKind, HistoryEntry[]> _history = new Dictionary<PlatformKind, HistoryEntry[]>();

        public PlatformKind CurrentPlatform { get; set; }
        public AccountState CurrentState => _states.ContainsKey(CurrentPlatform) ? _states[CurrentPlatform] : null;
        public HistoryEntry[] CurrentHistory => _history.ContainsKey(CurrentPlatform) ? _history[CurrentPlatform] : null;

        public static AccountManager Instance { get; private set; }

        public string Status { get; private set; }
        public bool Ready => Status == "ok";
        public bool Refreshing => _pendingRequestCount > 0;

        private Phantasma.SDK.PhantasmaAPI phantasmaApi;
        private Phantasma.Neo.Core.NeoAPI neoApi;

        private const string cryptoCompareAPIKey = "50f6f9f5adbb0a2f0d60145e43fe873c5a7ea1d8221b210ba14ef725f4012ee9";

        public static readonly PlatformKind[] AvailablePlatforms = new PlatformKind[] { PlatformKind.Phantasma, PlatformKind.Neo };

        private Dictionary<string, string> _currencyMap = new Dictionary<string, string>();
        public IEnumerable<string> Currencies => _currencyMap.Keys;

        private DateTime _lastPriceUpdate = DateTime.MinValue;

        private int _pendingRequestCount;

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
                return $"{price.ToString("0.####")} {ch}";
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
                    new Account() { name = "genesis", platforms = PlatformKind.Phantasma | PlatformKind.Neo, key = "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25", password = "lol", misc = "" },
                    //new Account() { name = "zion", platforms = PlatformKind.Neo, key = "KwVG94yjfVg1YKFyRxAGtug93wdRbmLnqqrFV6Yd2CiA9KZDAp4H", password = "", misc = "" },
                    new Account() { name = "master", platforms = PlatformKind.Neo, key = "KxDgvEKzgSBPPfuVfw67oPQBSjidEiqTHURKSDL1R7yGaGYAeYnr", password = "", misc = "" },
                    new Account() { name = "other", platforms = PlatformKind.Phantasma, key = "Kweyrx8ypkoPfzMsxV4NtgH8vXCWC1s1Dn3c2KJ4WAzC5nkyNt3e", password = "", misc = "" },
                    new Account() { name = "monk", platforms = PlatformKind.Phantasma, key = "Kx4GzZxzGZsQNt8URu36SnvR5KGSzg8s8ZxH8cunzZGh2JLmxHsW", password = "", misc = "" },
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
            neoApi = new NeoAPI(Settings.neoRPCURL, Settings.neoscanURL);

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

        public void SignAndSendTransaction(string chain, byte[] script, Action<Hash> callback)
        {
            var account = this.CurrentAccount;

            if (CurrentPlatform == PlatformKind.Phantasma)
            {
                var keys = PhantasmaKeys.FromWIF(account.key);
                StartCoroutine(phantasmaApi.SignAndSendTransaction(keys, script, chain, (hashText) =>
                {
                    var hash = Hash.Parse(hashText);
                    callback(hash);
                }, (error, msg) =>
                {
                    callback(Hash.Null);
                }));
            }
            else
            {
                callback(Hash.Null);
            }
        }

        private Action _refreshCallback;
        private DateTime _lastBalanceRefresh = DateTime.MinValue;
        private DateTime _lastHistoryRefresh = DateTime.MinValue;

        public void SelectAccount(int index)
        {
            _lastBalanceRefresh = DateTime.MinValue;
            _lastHistoryRefresh = DateTime.MinValue;
            _selectedAccountIndex = index;
            CurrentPlatform = PlatformKind.None;
            _states.Clear();
        }

        public void UnselectAcount()
        {
            _selectedAccountIndex = -1;
        }
        
        private void ReportWalletBalance(PlatformKind platform, AccountState state)
        {
            _pendingRequestCount--;

            if (state != null)
            {
                Debug.Log("Received new state for " + platform);
                _states[platform] = state;

                if (CurrentPlatform == PlatformKind.None)
                {
                    CurrentPlatform = platform;
                }
            }

            if (_pendingRequestCount == 0)
            {
                InvokeRefreshCallback();
            }
        }

        private void ReportWalletHistory(PlatformKind platform, List<HistoryEntry> history)
        {
            _pendingRequestCount--;

            if (history != null)
            {
                Debug.Log("Received new history for " + platform);
                _history[platform] = history.ToArray();

                if (CurrentPlatform == PlatformKind.None)
                {
                    CurrentPlatform = platform;
                }
            }

            if (_pendingRequestCount == 0)
            {
                InvokeRefreshCallback();
            }
        }

        public void RequestConfirmation(string transactionHash, Action<string> callback)
        {
            switch (CurrentPlatform)
            {
                case PlatformKind.Phantasma:
                    StartCoroutine( phantasmaApi.GetTransaction(transactionHash, (tx) =>
                    {
                        callback(null);
                    }, (error, msg) =>
                    {
                        callback(msg);
                    }));
                    break;

                default:
                    callback("platform not supported: " + CurrentPlatform);
                    break;
            }

        }

        private void InvokeRefreshCallback()
        {
            var temp = _refreshCallback;
            _refreshCallback = null;
            temp?.Invoke();
        }

        public void RefreshBalances(bool force, Action callback = null)
        {
            var now = DateTime.UtcNow;
            var diff = now - _lastBalanceRefresh;

            if (!force && diff.TotalSeconds < 30)
            {
                InvokeRefreshCallback();
                return;
            }

            _lastBalanceRefresh = now;
            _refreshCallback = callback;

            var platforms = CurrentAccount.platforms.Split();
            _pendingRequestCount = platforms.Count;

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

                                ReportWalletBalance(platform, state);
                            },
                            (error, msg) =>
                            {
                                ReportWalletBalance(platform, null);
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
                                    address = keys.Address,
                                    name = keys.Address, // TODO support NNS
                                    stake = 0,
                                    claim = 0, // TODO support claimable GAS
                                    balances = balances.ToArray(),
                                    flags = AccountFlags.None
                                };

                                if (state.stake > 50000)
                                {
                                    state.flags |= AccountFlags.Master;
                                }

                                ReportWalletBalance(platform, state);
                            }));
                        }
                        break;

                    default:
                        ReportWalletBalance(platform, null);
                        break;
                }
            }
        }

        public void RefreshHistory(bool force, Action callback = null)
        {
            var now = DateTime.UtcNow;
            var diff = now - _lastHistoryRefresh;

            if (!force && diff.TotalSeconds < 30)
            {
                InvokeRefreshCallback();
                return;
            }

            _lastBalanceRefresh = now;
            _refreshCallback = callback;

            var platforms = CurrentAccount.platforms.Split();
            _pendingRequestCount = platforms.Count;

            var account = this.CurrentAccount;

            foreach (var platform in platforms)
            {
                switch (platform)
                {
                    case PlatformKind.Phantasma:
                        {
                            var keys = PhantasmaKeys.FromWIF(account.key);
                            StartCoroutine(phantasmaApi.GetAddressTransactions(keys.Address.Text, 1, 20, (x, page, max) =>
                            {
                                var history = new List<HistoryEntry>();

                                foreach (var tx in x.txs)
                                {
                                    var timeSpan = TimeSpan.FromSeconds(tx.timestamp);
                                    history.Add(new HistoryEntry()
                                    {
                                        hash = tx.hash,
                                        date = new DateTime(timeSpan.Ticks).ToLocalTime(),
                                    });
                                }

                                ReportWalletHistory(platform, history);
                            },
                            (error, msg) =>
                            {
                                ReportWalletHistory(platform, null);
                            }));
                        }
                        break;

                    case PlatformKind.Neo:
                        {
                            var keys = NeoKey.FromWIF(account.key);
                            var url = GetNeoscanAPIUrl($"get_address_abstracts/{keys.Address}/1");

                            StartCoroutine(WebClient.RESTRequest(url, (error, msg) =>
                            {
                                ReportWalletHistory(platform, null);
                            },
                            (response) =>
                            {
                                var history = new List<HistoryEntry>();

                                var entries = response.GetNode("entries");
                                foreach (var entry in entries.Children)
                                {
                                    var hash = entry.GetString("txid");
                                    var time = entry.GetUInt32("time");
                                    var timeSpan = TimeSpan.FromSeconds(time);

                                    history.Add(new HistoryEntry()
                                    {
                                        hash = hash,
                                        date = new DateTime(timeSpan.Ticks).ToLocalTime(),
                                        url = GetNeoscanTransactionURL(hash),
                                    });
                                }

                                ReportWalletHistory(platform, history);
                            }));
                        }
                        break;

                    default:
                        ReportWalletHistory(platform, null);
                        break;
                }
            }
        }

        private string GetNeoscanTransactionURL(string hash)
        {
            var url = Settings.neoscanURL;
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            return $"{url}transaction/{hash}";
        }

        private string GetNeoscanAPIUrl(string request)
        {
            var protocol = Settings.neoscanURL.StartsWith("https") ? "https://" : "http://";

            var url = Settings.neoscanURL.Substring(protocol.Length);

            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            return $"{url}api/main_net/v1/{request}";
        }

        internal bool SwapSupported(string symbol)
        {
            return symbol == "SOUL" || symbol == "NEO" || symbol == "GAS";
        }
    }
}
