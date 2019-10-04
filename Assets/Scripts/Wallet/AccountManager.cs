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
using Phantasma.Domain;
using Phantasma.Core;
using Phantasma.Core.Utils;

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
        public string WIF;
        public string password;
        public string misc;

        public override string ToString()
        {
            return $"{name.ToUpper()} [{platforms}]";
        }

        public string GetAddress(PlatformKind platform)
        {
            if (!platforms.HasFlag(platform))
            {
                return null;
            }

            switch (platform)
            {
                case PlatformKind.Phantasma:
                    {
                        var keys = PhantasmaKeys.FromWIF(WIF);
                        return keys.Address.Text;
                    }

                case PlatformKind.Neo:
                    {
                        var keys = NeoKeys.FromWIF(WIF);
                        return keys.Address;
                    }

                default:
                    return null;
            }
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

        public static PlatformKind GetTransferTargets(this PlatformKind kind, Token token)
        {
            if (!token.flags.Contains("External"))
            {
                return kind;
            }

            switch (kind)
            {
                case PlatformKind.Phantasma:
                    return PlatformKind.Phantasma | PlatformKind.Neo;

                case PlatformKind.Neo:
                    return PlatformKind.Phantasma | PlatformKind.Neo;

                default:
                    return PlatformKind.None;
            }
        }
    }

    public struct TransferRequest
    {
        public PlatformKind platform;
        public string key;
        public string destination;
        public string symbol;
        public decimal amount;
        public string interop;
    }

    public class AccountState
    {
        public string name;
        public string address;
        public Balance[] balances;
        public AccountFlags flags;

        public decimal GetAvailableAmount(string symbol)
        {
            for (int i = 0; i < balances.Length; i++)
            {
                var entry = balances[i];
                if (entry.Symbol == symbol)
                {
                    return entry.Available;
                }
            }

            return 0;
        }
    }

    public class Balance
    {
        public string Symbol;
        public decimal Available;
        public decimal Staked;
        public decimal Pending;
        public decimal Claimable;
        public string Chain;
        public int Decimals;
        public string PendingPlatform;
        public string PendingHash;

        public decimal Total => Available + Staked + Pending + Claimable;
    }

    public class AccountManager : MonoBehaviour
    {
        public static readonly int MinGasLimit = 800;

        public Settings Settings { get; private set; }

        public Account[] Accounts { get; private set; }

        private Dictionary<string, Token> _tokenSymbolMap = null;
        private Dictionary<string, Token> _tokenHashMap = null;
        private Dictionary<string, decimal> _tokenPrices = new Dictionary<string, decimal>();
        public string CurrentTokenCurrency { get; private set; }

        private int _selectedAccountIndex;
        public int CurrentIndex => _selectedAccountIndex;
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

        public static readonly int SoulMasterStakeAmount = 50000;

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
                return $"{price.ToString(WalletGUI.MoneyFormat)} {ch}";
            }
            else
            {
                return "-";
            }
        }

        private IEnumerator FetchTokenPrices(IEnumerable<string> symbols, string currency)
        {
            var symbolList = string.Join(",", symbols);
            var url = $"https://min-api.cryptocompare.com/data/pricemulti?fsyms={symbolList}&tsyms={currency}&api_key={cryptoCompareAPIKey}";
            return WebClient.RESTRequest(url, (error, msg) =>
            {

            },
            (response) =>
            {
                try
                {
                    foreach (var symbol in symbols)
                    {
                        var node = response.GetNode(symbol);
                        var price = node.GetDecimal(currency);
                        SetTokenPrice(symbol, price);

                        if (symbol == "SOUL")
                        {
                            SetTokenPrice("KCAL", price / 5);
                        }
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

        private const string WalletTag = "wallet.list";

        // Start is called before the first frame update
        void Start()
        {
            Settings.Load();

            LoadNexus();

            var wallets = PlayerPrefs.GetString(WalletTag, "");

            if (!string.IsNullOrEmpty(wallets))
            {
                var bytes = Base16.Decode(wallets);
                Accounts = Serialization.Unserialize<Account[]>(bytes);
            }
            else
            {
                Accounts = new Account[] { };
            }
        }

        public void SaveAccounts()
        {
            var bytes = Serialization.Serialize(Accounts);
            PlayerPrefs.SetString(WalletTag, Base16.Encode(bytes));
            PlayerPrefs.Save();
        }

        private const string TokenInfoTag = "info.tokens";

        private void PrepareTokens(Token[] tokenArray)
        {
            var tokens = tokenArray.ToList();

            var nep5Flags = TokenFlags.Fungible.ToString() + "," + TokenFlags.External.ToString();
            tokens.Add(new Token() { symbol = "SOUL", hash = "ed07cffad18f1308db51920d99a2af60ac66a7b3", decimals = 8, maxSupply = "100000000", name = "Phantasma Stake", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "KCAL", hash = Hash.FromString("KCAL").ToString(), decimals = 10, maxSupply = "100000000", name = "Phantasma Energy", flags = TokenFlags.Fungible.ToString() });
            tokens.Add(new Token() { symbol = "NEO", hash = "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b", decimals = 0, maxSupply = "100000000", name = "Neo", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "GAS", hash = "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7", decimals = 8, maxSupply = "16580739", name = "GAS (Neo)", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "SWTH", hash = "ab38352559b8b203bde5fddfa0b07d8b2525e132", decimals = 8, maxSupply = "1000000000", name = "Switcheo", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "NEX", hash = "3a4acd3647086e7c44398aac0349802e6a171129", decimals = 8, maxSupply = "56460100", name = "Nex", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "PKC", hash = "af7c7328eee5a275a3bcaee2bf0cf662b5e739be", decimals = 8, maxSupply = "111623273", name = "Pikcio Token", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "NOS", hash = "c9c0fc5a2b66a29d6b14601e752e6e1a445e088d", decimals = 8, maxSupply = "710405560", name = "nOS", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "MKNI", hash = Hash.FromString("MKNI").ToString(), decimals = 0, maxSupply = "1000000", name = "Mankini", flags = TokenFlags.Fungible.ToString() });
            tokens.Add(new Token() { symbol = "NACHO", hash = Hash.FromString("NACHO").ToString(), decimals = 8, maxSupply = "1000000", name = "Nachos", flags = TokenFlags.Fungible.ToString() });

            CurrentTokenCurrency = "";

            _tokenSymbolMap = new Dictionary<string, Token>();
            _tokenHashMap = new Dictionary<string, Token>();
            foreach (var token in tokens)
            {
                _tokenSymbolMap[token.symbol] = token;
                _tokenHashMap[token.hash] = token;
            }

            Debug.Log($"{_tokenSymbolMap.Count} tokens supported");
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

                var expectedFlag = TokenFlags.External.ToString();
                var symbolList = _tokenSymbolMap.Values.Where(x => x.flags.Contains(expectedFlag)).Select(x => x.symbol);
                StartCoroutine(FetchTokenPrices(symbolList, CurrentTokenCurrency));
            }
        }

        private void LoadNexus()
        {
            phantasmaApi = new PhantasmaAPI(Settings.phantasmaRPCURL);
            neoApi = new NeoAPI(Settings.neoRPCURL, Settings.neoscanURL);

            PrepareTokens(new Token[] { });

            /*var tokenList = PlayerPrefs.GetString(TokenInfoTag, "");

            if (!string.IsNullOrEmpty(tokenList))
            {
                var tokenBytes = Base16.Decode(tokenList);
                    
                var tokens = Serialization.Unserialize<Token[]>(tokenBytes);

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
            }));*/
        }

        // Update is called once per frame
        void Update()
        {

        }

        public int GetTokenDecimals(string symbol)
        {
            if (_tokenSymbolMap.ContainsKey(symbol))
            {
                return _tokenSymbolMap[symbol].decimals;
            }

            return -1;
        }

        public bool GetTokenBySymbol(string symbol, out Token token)
        {
            if (_tokenSymbolMap.ContainsKey(symbol))
            {
                token = _tokenSymbolMap[symbol];
                return true;
            }

            token = new Token();
            return false;
        }

        public bool GetTokenByHash(string hash, out Token token)
        {
            if (_tokenHashMap.ContainsKey(hash))
            {
                token = _tokenHashMap[hash];
                return true;
            }

            token = new Token();
            return false;
        }

        public decimal AmountFromString(string str, int decimals)
        {
            if (string.IsNullOrEmpty(str))
            {
                return 0;
            }

            var n = BigInteger.Parse(str);
            return UnitConversion.ToDecimal(n, decimals);
        }

        public void SignAndSendTransaction(string chain, byte[] script, Action<Hash> callback)
        {
            var account = this.CurrentAccount;

            switch (CurrentPlatform)
            {
                case PlatformKind.Phantasma:
                    {
                        var keys = PhantasmaKeys.FromWIF(account.WIF);
                        StartCoroutine(phantasmaApi.SignAndSendTransaction(keys, script, chain, (hashText) =>
                        {
                            var hash = Hash.Parse(hashText);
                            callback(hash);
                        }, (error, msg) =>
                        {
                            callback(Hash.Null);
                        }));
                        break;
                    }

                default:
                    {
                        try
                        {
                            var transfer = Serialization.Unserialize<TransferRequest>(script);

                            if (transfer.amount <=0)
                            {
                                callback(Hash.Null);
                            }
                            else
                            if (transfer.platform == CurrentPlatform)
                            {
                                switch (transfer.platform)
                                {
                                    case PlatformKind.Neo:
                                        {
                                            var keys = NeoKeys.FromWIF(transfer.key);

                                            StartCoroutine(neoApi.GetUnspent(keys.Address, (unspent) =>
                                            {
                                                Debug.Log("Got unspents for " + keys.Address);

                                                if (transfer.symbol == "NEO" || transfer.symbol == "GAS")
                                                {
                                                    StartCoroutine(neoApi.SendAsset((tx) =>
                                                    {
                                                        if (tx != null)
                                                        {
                                                            var hash = Hash.Parse(tx.Hash.ToString());
                                                            callback(hash);
                                                        }
                                                        else
                                                        {
                                                            callback(Hash.Null);
                                                        }
                                                    }, unspent, keys, transfer.destination, transfer.symbol, transfer.amount, transfer.interop)
                                                    );
                                                }
                                                else
                                                {
                                                    Token token;

                                                    if (GetTokenBySymbol(transfer.symbol, out token))
                                                    {
                                                        var amount = System.Numerics.BigInteger.Parse(UnitConversion.ToBigInteger(transfer.amount, token.decimals).ToString());

                                                        var nep5 = new NEP5(neoApi, token.hash);
                                                        StartCoroutine(nep5.Transfer(unspent, keys, transfer.destination, amount, transfer.interop,
                                                        (tx) =>
                                                        {
                                                            if (tx != null)
                                                            {
                                                                var hash = Hash.Parse(tx.Hash.ToString());
                                                                callback(hash);
                                                            }
                                                            else
                                                            {
                                                                callback(Hash.Null);
                                                            }
                                                        }));
                                                    }
                                                    else
                                                    {
                                                        callback(Hash.Null);
                                                    }
                                                }

                                            }));

                                            break;
                                        }
                                }
                                return;
                            }
                        }
                        catch
                        {
                            // just continue
                        }

                        callback(Hash.Null);
                        break;
                    }
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

        private const string TempConfirmError = "Something went wrong when confirming.\nThe transaction might have been succesful.\nCheck back later.";

        public void RequestConfirmation(string transactionHash, Action<string> callback)
        {
            switch (CurrentPlatform)
            {
                case PlatformKind.Phantasma:
                    StartCoroutine(phantasmaApi.GetTransaction(transactionHash, (tx) =>
                    {
                        callback(null);
                    }, (error, msg) =>
                    {
                        callback(msg);
                    }));
                    break;

                case PlatformKind.Neo:
                    var url = GetNeoscanAPIUrl($"get_transaction/{transactionHash}");

                    StartCoroutine(WebClient.RESTRequest(url, (error, msg) =>
                    {
                        callback(TempConfirmError);
                    },
                    (response) =>
                    {
                        if (response.HasNode("vouts"))
                        {
                            callback(null);
                        }
                        else
                        {
                            callback(TempConfirmError);
                        }
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
                            var keys = PhantasmaKeys.FromWIF(account.WIF);
                            StartCoroutine(phantasmaApi.GetAccount(keys.Address.Text, (acc) =>
                            {
                                var balanceMap = new Dictionary<string, Balance>();

                                foreach (var entry in acc.balances)
                                {
                                    balanceMap[entry.symbol] = new Balance()
                                    {
                                        Symbol = entry.symbol,
                                        Available = AmountFromString(entry.amount, GetTokenDecimals(entry.symbol)),
                                        Pending = 0,
                                        Staked = 0,
                                        Claimable = 0,
                                        Chain = entry.chain,
                                        Decimals = GetTokenDecimals(entry.symbol)
                                    };
                                }

                                var stakedAmount = AmountFromString(acc.stake, GetTokenDecimals("SOUL"));
                                var claimableAmount = AmountFromString(acc.unclaimed, GetTokenDecimals("KCAL"));

                                if (stakedAmount > 0)
                                {
                                    var symbol = "SOUL";
                                    if (balanceMap.ContainsKey(symbol))
                                    {
                                        var entry = balanceMap[symbol];
                                        entry.Staked = stakedAmount;
                                    }
                                    else
                                    {
                                        var entry = new Balance()
                                        {
                                            Symbol = symbol,
                                            Chain = "main",
                                            Available = 0,
                                            Staked = stakedAmount,
                                            Claimable = 0,
                                            Pending = 0,
                                            Decimals = GetTokenDecimals(symbol)
                                        };
                                        balanceMap[symbol] = entry;
                                    }
                                }

                                if (claimableAmount > 0)
                                {
                                    var symbol = "KCAL";
                                    if (balanceMap.ContainsKey(symbol))
                                    {
                                        var entry = balanceMap[symbol];
                                        entry.Claimable = claimableAmount;
                                    }
                                    else
                                    {
                                        var entry = new Balance()
                                        {
                                            Symbol = symbol,
                                            Chain = "main",
                                            Available = 0,
                                            Staked = 0,
                                            Claimable = claimableAmount,
                                            Pending = 0,
                                            Decimals = GetTokenDecimals(symbol)
                                        };
                                        balanceMap[symbol] = entry;
                                    }
                                }

                                RequestPendings(keys.Address.Text, (swaps, error) =>
                                {
                                    if (swaps != null)
                                    {
                                        MergeSwaps(DomainSettings.PlatformName, balanceMap, swaps);
                                    }
                                    else
                                    {
                                        Debug.LogWarning(error);
                                    }


                                    var state = new AccountState()
                                    {
                                        address = acc.address,
                                        name = acc.name,
                                        balances = balanceMap.Values.ToArray(),
                                        flags = AccountFlags.None
                                    };

                                    if (stakedAmount >= SoulMasterStakeAmount)
                                    {
                                        state.flags |= AccountFlags.Master;
                                    }
                                    ReportWalletBalance(platform, state);
                                });
                            },
                            (error, msg) =>
                            {
                                ReportWalletBalance(platform, null);
                            }));
                        }
                        break;

                    case PlatformKind.Neo:
                        {
                            var keys = NeoKeys.FromWIF(account.WIF);

                            var url = GetNeoscanAPIUrl($"get_balance/{keys.Address}");

                            StartCoroutine(WebClient.RESTRequest(url, (error, msg) =>
                            {
                                ReportWalletBalance(platform, null);
                            },
                            (response) =>
                            {
                                var balances = new List<Balance>();

                                var balance = response.GetNode("balance");
                                foreach (var entry in balance.Children)
                                {
                                    var hash = entry.GetString("asset_hash");
                                    var symbol = entry.GetString("asset_symbol");
                                    var amount = entry.GetDecimal("amount");

                                    Token token;

                                    if (GetTokenBySymbol(symbol, out token))
                                    {
                                        if (hash == token.hash)
                                        {
                                            balances.Add(new Balance()
                                            {
                                                Symbol = symbol,
                                                Available = amount,
                                                Pending = 0,
                                                Claimable = 0, // TODO support claimable GAS
                                                Staked = 0,
                                                Chain = "main",
                                                Decimals = token.decimals
                                            });
                                        }
                                    }
                                }

                                RequestPendings(keys.Address, (swaps, error) =>
                                {
                                    var balanceMap = new Dictionary<string, Balance>();
                                    foreach (var entry in balances)
                                    {
                                        balanceMap[entry.Symbol] = entry;
                                    }

                                    if (swaps != null)
                                    {
                                        MergeSwaps("neo", balanceMap, swaps);
                                    }
                                    else
                                    {
                                        Debug.LogWarning(error);
                                    }

                                    var state = new AccountState()
                                    {
                                        address = keys.Address,
                                        name = keys.Address, // TODO support NNS
                                        balances = balanceMap.Values.ToArray(),
                                        flags = AccountFlags.None
                                    };
                                    ReportWalletBalance(platform, state);
                                });

                            }));
                        }
                        break;

                    default:
                        ReportWalletBalance(platform, null);
                        break;
                }
            }
        }

        private void MergeSwaps(string platform, Dictionary<string, Balance> balanceMap, Swap[] swaps)
        {
            foreach (var swap in swaps)
            {
                if (swap.destinationPlatform != platform)
                {
                    continue;
                }

                if (swap.destinationHash != "pending")
                {
                    continue;
                }

                var decimals = GetTokenDecimals(swap.symbol);
                var amount = AmountFromString(swap.value, decimals);

                Debug.Log($"Found pending {platform} swap: {amount} {swap.symbol}");

                if (balanceMap.ContainsKey(swap.symbol))
                {
                    var entry = balanceMap[swap.symbol];
                    entry.Pending += amount;
                    entry.PendingHash = swap.sourceHash;
                    entry.PendingPlatform = swap.sourcePlatform;
                }
                else
                {
                    var entry = new Balance()
                    {
                        Symbol = swap.symbol,
                        Chain = "main",
                        Available = 0,
                        Staked = 0,
                        Claimable = 0,
                        Pending = amount,
                        Decimals = decimals,
                        PendingHash = swap.sourceHash,
                        PendingPlatform = swap.sourcePlatform,
                    };
                    balanceMap[swap.symbol] = entry;
                }
            }
        }

        internal void InitDemoAccounts(NexusKind nexusKind)
        {
            var accounts = new List<Account>();

            if (nexusKind != NexusKind.Main_Net)
            {
                accounts.Add(new Account() { name = "genesis", platforms = PlatformKind.Phantasma | PlatformKind.Neo, WIF = "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25", password = "lol", misc = "" });
                accounts.Add(new Account() { name = "bill", platforms = PlatformKind.Neo, WIF = "KxDgvEKzgSBPPfuVfw67oPQBSjidEiqTHURKSDL1R7yGaGYAeYnr", password = "mankini", misc = "" });
            }
            //new Account() { name = "zion", platforms = PlatformKind.Neo, key = "KwVG94yjfVg1YKFyRxAGtug93wdRbmLnqqrFV6Yd2CiA9KZDAp4H", password = "", misc = "" },

            if (nexusKind == NexusKind.Local_Net)
            {
                accounts.Add(new Account() { name = "other", platforms = PlatformKind.Phantasma | PlatformKind.Neo, WIF = "Kweyrx8ypkoPfzMsxV4NtgH8vXCWC1s1Dn3c2KJ4WAzC5nkyNt3e", password = "", misc = "" });
                accounts.Add(new Account() { name = "monk", platforms = PlatformKind.Phantasma | PlatformKind.Neo, WIF = "Kx4GzZxzGZsQNt8URu36SnvR5KGSzg8s8ZxH8cunzZGh2JLmxHsW", password = "", misc = "" });
            }

            this.Accounts = accounts.ToArray();
            SaveAccounts();
        }

        internal void DeleteAll()
        {
            this.Accounts = new Account[0];
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
                            var keys = PhantasmaKeys.FromWIF(account.WIF);
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
                            var keys = NeoKeys.FromWIF(account.WIF);
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

        private void RequestPendings(string address, Action<Swap[], string> callback)
        {
            StartCoroutine(phantasmaApi.GetSwapsForAddress(address, (swaps) =>
            {
                callback(swaps, null);
            }, (error, msg) =>
            {
                callback(null, msg);
            }));
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

        public int AddWallet(string name, PlatformKind platforms, string wif, string password)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 3)
            {
                throw new Exception("Name is too short.");
            }

            if (name.Length > 16)
            {
                throw new Exception("Name is too long.");
            }

            for (int i = 0; i < Accounts.Length; i++)
            {
                if (Accounts[i].name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("An account with this name already exists.");
                }
            }

            var list = this.Accounts.ToList();
            list.Add(new Account() { name = name, WIF = wif, password = password, platforms = platforms, misc = "" });

            this.Accounts = list.ToArray();
            return Accounts.Length - 1;
        }

        public static Address EncodeNeoAddress(string addressText)
        {
            Throw.If(!Phantasma.Neo.Utils.NeoUtils.IsValidAddress(addressText), "invalid neo address");
            var scriptHash = addressText.Base58CheckDecode();

            var pubKey = new byte[33];
            ByteArrayUtils.CopyBytes(scriptHash, 0, pubKey, 0, scriptHash.Length);

            return Address.FromInterop(1/*NeoID*/, pubKey);
        }

        public static string DecodeNeoInteropAddress(Address address)
        {
            if (!address.IsInterop)
            {
                throw new Exception("not an interop address");
            }

            byte platformID;
            byte[] scriptHash;
            address.DecodeInterop(out platformID, out scriptHash);

            if (scriptHash[0] != 23)
            {
                throw new Exception("invalid NEO address");
            }

            scriptHash = scriptHash.Take(21).ToArray();

            return scriptHash.Base58CheckEncode();
        }

        private Dictionary<PlatformKind, string> _interopMap = new Dictionary<PlatformKind, string>();

        internal void FindInteropAddress(PlatformKind platform, Action<string> callback)
        {
            if (_interopMap.ContainsKey(platform))
            {
                callback(_interopMap[platform]);
                return;
            }

            StartCoroutine(phantasmaApi.GetPlatforms((platforms) =>
            {
                var platformName = platform.ToString().ToLower();
                foreach (var entry in platforms)
                {
                    if (entry.platform == platformName)
                    {
                        string interopAddress = entry.interop[0].external;
                        _interopMap[platform] = interopAddress;
                        callback(interopAddress);
                        return;
                    }
                }

                callback(null);
            }, (error, msg) =>
            {
                callback(null);
            }));
        }

        internal void SettleSwap(string sourcePlatform, string destPlatform, string pendingHash, Action<Hash> callback)
        {
            StartCoroutine(phantasmaApi.SettleSwap(sourcePlatform, destPlatform, pendingHash, (hash) =>
            {
                callback(Hash.Parse(hash));
            }, (msg, error) =>
            {
                Debug.LogWarning(msg);
                callback(Hash.Null);
            }));
        }

        internal void DeleteAccount(int currentIndex)
        {
            if (currentIndex<0 || currentIndex >= Accounts.Length)
            {
                return;
            }

            var temp = Accounts.ToList();
            temp.RemoveAt(currentIndex);
            this.Accounts = temp.ToArray();
            SaveAccounts();
        }

        public void RenameAccount(string newName)
        {
            Accounts[CurrentIndex].name = newName;
            SaveAccounts();
        }
    }
}
