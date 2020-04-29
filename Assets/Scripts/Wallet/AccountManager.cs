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
using Phantasma.Core.Types;
using LunarLabs.Parser;

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
            if (!token.flags.Contains("Foreign"))
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
        public PlatformKind platform;
        public string name;
        public string address;
        public Balance[] balances;
        public AccountFlags flags;
        public Timestamp stakeTime;

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
        public string[] Ids;

        public decimal Total => Available + Staked + Pending + Claimable;
    }

    public class AccountManager : MonoBehaviour
    {
        public string WalletIdentifier => "PGT" + UnityEngine.Application.version;

        public static readonly int MinGasLimit = 800;

        public Settings Settings { get; private set; }

        public Account[] Accounts { get; private set; }

        private Dictionary<string, Token> _tokenSymbolMap = null;
        private Dictionary<string, Token> _tokenCryptoCompareSymbolMap = null;
        private Dictionary<string, Token> _tokenHashMap = null;
        private Dictionary<string, decimal> _tokenPrices = new Dictionary<string, decimal>();
        public string CurrentTokenCurrency { get; private set; }

        private int _selectedAccountIndex;
        public int CurrentIndex => _selectedAccountIndex;
        public Account CurrentAccount => HasSelection ? Accounts[_selectedAccountIndex] : new Account() { };

        public bool HasSelection => _selectedAccountIndex >= 0 && _selectedAccountIndex < Accounts.Length;

        private Dictionary<PlatformKind, AccountState> _states = new Dictionary<PlatformKind, AccountState>();
        private Dictionary<PlatformKind, List<TokenData>> _ttrsNft = new Dictionary<PlatformKind, List<TokenData>>();
        private Dictionary<PlatformKind, HistoryEntry[]> _history = new Dictionary<PlatformKind, HistoryEntry[]>();

        public PlatformKind CurrentPlatform { get; set; }
        public AccountState CurrentState => _states.ContainsKey(CurrentPlatform) ? _states[CurrentPlatform] : null;
        public List<TokenData> CurrentTtrsNft => _ttrsNft.ContainsKey(CurrentPlatform) ? _ttrsNft[CurrentPlatform] : null;
        public HistoryEntry[] CurrentHistory => _history.ContainsKey(CurrentPlatform) ? _history[CurrentPlatform] : null;

        public static AccountManager Instance { get; private set; }

        public string Status { get; private set; }
        public bool Ready => Status == "ok";
        public bool Refreshing => _pendingRequestCount > 0;

        public Phantasma.SDK.PhantasmaAPI phantasmaApi { get; private set; }
        private Phantasma.Neo.Core.NeoAPI neoApi;

        private const string cryptoCompareAPIKey = "50f6f9f5adbb0a2f0d60145e43fe873c5a7ea1d8221b210ba14ef725f4012ee9";

        public static readonly PlatformKind[] AvailablePlatforms = new PlatformKind[] { PlatformKind.Phantasma, PlatformKind.Neo };

        private Dictionary<string, string> _currencyMap = new Dictionary<string, string>();
        public IEnumerable<string> Currencies => _currencyMap.Keys;

        public static readonly int SoulMasterStakeAmount = 50000;

        private DateTime _lastPriceUpdate = DateTime.MinValue;

        private int _pendingRequestCount;

        private bool _accountInitialized;

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
                    foreach (var cryptoCompareSymbol in symbols)
                    {
                        var node = response.GetNode(cryptoCompareSymbol);
                        if (node != null)
                        {
                            var price = node.GetDecimal(currency);

                            var symbol = GetTokenSymbolByCryptoCompareSymbol(cryptoCompareSymbol);
                            SetTokenPrice(symbol, price);

                            if (symbol == "SOUL")
                            {
                                SetTokenPrice("KCAL", price / 5);
                            }
                        }
                        else
                        {
                            Log.Write($"Cannot get price for '{cryptoCompareSymbol}'.");
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.WriteWarning(e.ToString());
                }
            });
        }

        private void SetTokenPrice(string symbol, decimal price)
        {
            Log.Write($"Got price for {symbol} => {price}");
            _tokenPrices[symbol] = price;
        }

        private const string WalletTag = "wallet.list";

        public void UpdateRPCURL()
        {
            Settings.phantasmaRPCURL = Settings.phantasmaBPURL;

            if (Settings.nexusKind == NexusKind.Custom)
                return; // No need to change RPC, it is set by custom settings.

            if (Settings.nexusName != "mainnet")
            {
                return; // HACK getpeers only for mainnet
            }

            var url = $"https://ghostdevs.com/getpeers.json";

            StartCoroutine(
                WebClient.RESTRequest(url, (error, msg) =>
                {
                    Log.Write("auto error => " + error);
                },
                (response) =>
                {
                    var index = ((int)(Time.realtimeSinceStartup * 1000)) % response.ChildCount;
                    var node = response.GetNodeByIndex(index);
                    var result = node.GetString("url") + "/rpc";

                    Settings.phantasmaRPCURL = result;
                    Log.Write($"changed RPC url {index} => {result}");
                    UpdateAPIs();
                })
            );
        }

        // Start is called before the first frame update
        void Start()
        {
            Settings.Load();

            UpdateRPCURL();

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

            var nep5Flags = TokenFlags.Transferable.ToString() + "," + TokenFlags.Fungible.ToString() + "," + TokenFlags.Foreign.ToString()+ ","+TokenFlags.Divisible.ToString();
            var pepFlags = TokenFlags.Transferable.ToString() + "," + TokenFlags.Fungible.ToString();
            var nftFlags = TokenFlags.None.ToString();
            tokens.Add(new Token() { symbol = "SOUL", cryptoCompareSymbol = "GOST", hash = "ed07cffad18f1308db51920d99a2af60ac66a7b3", decimals = 8, maxSupply = "100000000", name = "Phantasma Stake", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "KCAL", cryptoCompareSymbol = "KCAL", hash = Hash.FromString("KCAL").ToString(), decimals = 10, maxSupply = "100000000", name = "Phantasma Energy", flags = TokenFlags.Transferable.ToString() + "," + TokenFlags.Fungible.ToString() + ","+ TokenFlags.Divisible.ToString() });
            tokens.Add(new Token() { symbol = "NEO", cryptoCompareSymbol = "NEO", hash = "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b", decimals = 0, maxSupply = "100000000", name = "Neo", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "GAS", cryptoCompareSymbol = "GAS", hash = "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7", decimals = 8, maxSupply = "16580739", name = "GAS (Neo)", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "SWTH", cryptoCompareSymbol = "SWTH", hash = "ab38352559b8b203bde5fddfa0b07d8b2525e132", decimals = 8, maxSupply = "1000000000", name = "Switcheo", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "NEX", cryptoCompareSymbol = "NEX", hash = "3a4acd3647086e7c44398aac0349802e6a171129", decimals = 8, maxSupply = "56460100", name = "Nex", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "PKC", cryptoCompareSymbol = "PKC", hash = "af7c7328eee5a275a3bcaee2bf0cf662b5e739be", decimals = 8, maxSupply = "111623273", name = "Pikcio Token", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "NOS", cryptoCompareSymbol = "NOS", hash = "c9c0fc5a2b66a29d6b14601e752e6e1a445e088d", decimals = 8, maxSupply = "710405560", name = "nOS", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "MKNI", cryptoCompareSymbol = "MKNI", hash = Hash.FromString("MKNI").ToString(), decimals = 0, maxSupply = "1000000", name = "Mankini", flags = pepFlags });
            tokens.Add(new Token() { symbol = "NACHO", cryptoCompareSymbol = "NACHO", hash = Hash.FromString("NACHO").ToString(), decimals = 8, maxSupply = "1000000", name = "Nachos", flags = pepFlags });
            tokens.Add(new Token() { symbol = "TTRS", cryptoCompareSymbol = "TTRS", hash = Hash.FromString("TTRS").ToString(), decimals = 0, maxSupply = "1000000", name = "22series", flags = nftFlags });
            tokens.Add(new Token() { symbol = "GOATI", cryptoCompareSymbol = "GOATI", hash = Hash.FromString("GOATI").ToString(), decimals = 3, maxSupply = "1000000", name = "GOATi", flags = pepFlags + "," + TokenFlags.Divisible.ToString() });
            tokens.Add(new Token() { symbol = "TKY", cryptoCompareSymbol = "TKY", hash = "132947096727c84c7f9e076c90f08fec3bc17f18", decimals = 8, maxSupply = "1000000000", name = "The Key", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "CGAS", cryptoCompareSymbol = "CGAS", hash = "74f2dc36a68fdc4682034178eb2220729231db76", decimals = 8, maxSupply = "1000000000", name = "NEP5 GAS", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "MCT", cryptoCompareSymbol = "MCT", hash = "a87cc2a513f5d8b4a42432343687c2127c60bc3f", decimals = 8, maxSupply = "1000000000", name = "Master Contract", flags = nep5Flags });

            tokens.Add(new Token() { symbol = "DBC", cryptoCompareSymbol = "DBC", hash = "b951ecbbc5fe37a9c280a76cb0ce0014827294cf", decimals = 8, maxSupply = "1000000000", name = "DeepBrain Coin", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "FTW", cryptoCompareSymbol = "FTW", hash = "11dbc2316f35ea031449387f615d9e4b0cbafe8b", decimals = 8, maxSupply = "1000000000", name = "For The Win", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "ZPT", cryptoCompareSymbol = "ZPT", hash = "ac116d4b8d4ca55e6b6d4ecce2192039b51cccc5", decimals = 8, maxSupply = "1000000000", name = "Zeepin Token", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "ACAT", cryptoCompareSymbol = "ACAT", hash = "7f86d61ff377f1b12e589a5907152b57e2ad9a7a", decimals = 8, maxSupply = "1000000000", name = "Alphacat", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "QLC", cryptoCompareSymbol = "QLC", hash = "0d821bd7b6d53f5c2b40e217c6defc8bbe896cf5", decimals = 8, maxSupply = "1000000000", name = "Qlink Token", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "TNC", cryptoCompareSymbol = "TNC", hash = "08e8c4400f1af2c20c28e0018f29535eb85d15b6", decimals = 8, maxSupply = "1000000000", name = "Trinity Network Credit", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "PHX", cryptoCompareSymbol = "PHX", hash = "1578103c13e39df15d0d29826d957e85d770d8c9", decimals = 8, maxSupply = "1000000000", name = "Red Pulse Phoenix", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "APH", cryptoCompareSymbol = "APH", hash = "a0777c3ce2b169d4a23bcba4565e3225a0122d95", decimals = 8, maxSupply = "1000000000", name = "Aphelion", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "GALA", cryptoCompareSymbol = "GALA", hash = "9577c3f972d769220d69d1c4ddbd617c44d067aa", decimals = 8, maxSupply = "1000000000", name = "Galaxy Token", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "AVA", cryptoCompareSymbol = "AVA", hash = "de2ed49b691e76754c20fe619d891b78ef58e537", decimals = 8, maxSupply = "1000000000", name = "Travala", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "NKN", cryptoCompareSymbol = "NKN", hash = "c36aee199dbba6c3f439983657558cfb67629599", decimals = 8, maxSupply = "1000000000", name = "NKN", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "LRN", cryptoCompareSymbol = "LRN", hash = "06fa8be9b6609d963e8fc63977b9f8dc5f10895f", decimals = 8, maxSupply = "1000000000", name = "Loopring Neo Token", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "ASA", cryptoCompareSymbol = "ASA", hash = "a58b56b30425d3d1f8902034996fcac4168ef71d", decimals = 8, maxSupply = "1000000000", name = "Asura World Coin", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "OBT", cryptoCompareSymbol = "OBT", hash = "0e86a40588f715fcaf7acd1812d50af478e6e917", decimals = 8, maxSupply = "1000000000", name = "Orbis", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "NRVE", cryptoCompareSymbol = "NRVE", hash = "a721d5893480260bd28ca1f395f2c465d0b5b1c2", decimals = 8, maxSupply = "1000000000", name = "Narrative Token", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "RHT", cryptoCompareSymbol = "RHT", hash = "2328008e6f6c7bd157a342e789389eb034d9cbc4", decimals = 8, maxSupply = "1000000000", name = "HashPuppy Token", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "NOS", cryptoCompareSymbol = "NOS", hash = "c9c0fc5a2b66a29d6b14601e752e6e1a445e088d", decimals = 8, maxSupply = "1000000000", name = "Neo Operating System", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "LX", cryptoCompareSymbol = "LX", hash = "bb3b54ab244b3658155f2db4429fc38ac4cef625", decimals = 8, maxSupply = "1000000000", name = "Moonlight Lux", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "TOLL", cryptoCompareSymbol = "TOLL", hash = "78fd589f7894bf9642b4a573ec0e6957dfd84c48", decimals = 8, maxSupply = "1000000000", name = "Bridge Protocol", flags = nep5Flags });
            tokens.Add(new Token() { symbol = "", cryptoCompareSymbol = "", hash = "e9a85bc36e409e3f5cca5c43bc3bff338d7ada08", decimals = 8, maxSupply = "1000000000", name = "imusify Token", flags = nep5Flags });

            CurrentTokenCurrency = "";

            _tokenSymbolMap = new Dictionary<string, Token>();
            _tokenCryptoCompareSymbolMap = new Dictionary<string, Token>();
            _tokenHashMap = new Dictionary<string, Token>();
            foreach (var token in tokens)
            {
                _tokenSymbolMap[token.symbol] = token;
                _tokenCryptoCompareSymbolMap[token.cryptoCompareSymbol] = token;
                _tokenHashMap[token.hash] = token;
            }

            Log.Write($"{_tokenSymbolMap.Count} tokens supported");
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

                var expectedFlag = TokenFlags.Foreign.ToString();
                var symbolList = _tokenCryptoCompareSymbolMap.Values.Where(x => x.flags.Contains(expectedFlag)).Select(x => x.cryptoCompareSymbol);
                StartCoroutine(FetchTokenPrices(symbolList, CurrentTokenCurrency));
            }
        }

        public void UpdateAPIs()
        {
            Log.Write("reinit APIs => " + Settings.phantasmaRPCURL);
            phantasmaApi = new PhantasmaAPI(Settings.phantasmaRPCURL);
            neoApi = new NeoAPI(Settings.neoRPCURL, Settings.neoscanURL);
        }

        private void LoadNexus()
        {
            UpdateAPIs();

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

        public string GetTokenSymbolByCryptoCompareSymbol(string cryptoCompareSymbol)
        {
            if (_tokenCryptoCompareSymbolMap.ContainsKey(cryptoCompareSymbol))
            {
                return _tokenCryptoCompareSymbolMap[cryptoCompareSymbol].symbol;
            }

            return "";
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

        public void SignAndSendTransaction(string chain, byte[] script, byte[] payload, Action<Hash, string> callback)
        {
            var account = this.CurrentAccount;

            if (payload == null)
            {
                payload = System.Text.Encoding.UTF8.GetBytes(WalletIdentifier);
            }

            switch (CurrentPlatform)
            {
                case PlatformKind.Phantasma:
                    {
                        var keys = PhantasmaKeys.FromWIF(account.WIF);
                        StartCoroutine(phantasmaApi.SignAndSendTransactionWithPayload(keys, Settings.nexusName, script, chain, payload,  (hashText) =>
                        {
                            var hash = Hash.Parse(hashText);
                            callback(hash, null);
                        }, (error, msg) =>
                        {
                            callback(Hash.Null, msg);
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
                                callback(Hash.Null, $"invalid transfer amount: {transfer.amount}");
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
                                                Log.Write("Got unspents for " + keys.Address);

                                                if (transfer.symbol == "NEO" || transfer.symbol == "GAS")
                                                {
                                                    StartCoroutine(neoApi.SendAsset((tx, error) =>
                                                    {
                                                        if (tx != null)
                                                        {
                                                            var hash = Hash.Parse(tx.Hash.ToString());
                                                            callback(hash, null);
                                                        }
                                                        else
                                                        {
                                                            callback(Hash.Null, error);
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
                                                        CoroutineUtils.StartThrowingCoroutine(this, nep5.Transfer(unspent, keys, transfer.destination, amount, transfer.interop,
                                                        (tx, error) =>
                                                        {
                                                            if (tx != null)
                                                            {
                                                                var hash = Hash.Parse(tx.Hash.ToString());
                                                                callback(hash, null);
                                                            }
                                                            else
                                                            {
                                                                callback(Hash.Null, error);
                                                            }
                                                        }), ex => {
                                                            if (ex != null)
                                                            {
                                                                callback(Hash.Null, ex.ToString());
                                                            }
                                                        });
                                                    }
                                                    else
                                                    {
                                                        callback(Hash.Null, "invalid token: "+transfer.symbol);
                                                    }
                                                }

                                            }));

                                            break;
                                        }
                                }
                                return;
                            }
                        }
                        catch (Exception e)
                        {
                            callback(Hash.Null, e.ToString());
                            return;
                        }

                        callback(Hash.Null, "something weird happened");
                        break;
                    }
            }
        }

        private Action _refreshCallback;
        private DateTime _lastBalanceRefresh = DateTime.MinValue;
        private DateTime _lastTtrsNftRefresh = DateTime.MinValue;
        private DateTime _lastHistoryRefresh = DateTime.MinValue;

        public void SelectAccount(int index)
        {
            _lastBalanceRefresh = DateTime.MinValue;
            _lastTtrsNftRefresh = DateTime.MinValue;
            _lastHistoryRefresh = DateTime.MinValue;
            _selectedAccountIndex = index;

            _accountInitialized = false;

            var platforms = CurrentAccount.platforms.Split();
            CurrentPlatform = platforms.FirstOrDefault();
            _states.Clear();
        }

        public void UnselectAcount()
        {
            _selectedAccountIndex = -1;

            _accountInitialized = false;

            _states.Clear();
        }

        private void ReportWalletBalance(PlatformKind platform, AccountState state)
        {
            _pendingRequestCount--;

            if (state != null)
            {
                Log.Write("Received new state for " + platform);
                _states[platform] = state;

                if (!_accountInitialized && GetWorthOfPlatform(platform) > GetWorthOfPlatform(CurrentPlatform))
                {
                    CurrentPlatform = platform;
                }
            }

            if (_pendingRequestCount == 0)
            {
                _accountInitialized = true;
                InvokeRefreshCallback();
            }
        }

        private decimal GetWorthOfPlatform(PlatformKind platform)
        {
            if (!_states.ContainsKey(platform))
            {
                return 0;
            }

            decimal total = 0;
            var state = _states[platform];
            foreach (var balance in state.balances)
            {
                total += balance.Total;
            }
            return total;
        }

        private void ReportWalletTtrsNft(PlatformKind platform)
        {
            _pendingRequestCount--;

            if (_ttrsNft.ContainsKey(platform) && _ttrsNft[platform] != null)
            {
                Log.Write($"Received {_ttrsNft[platform].Count()} new TTRS NFTs for {platform}");

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
                Log.Write("Received new history for " + platform);
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


        private const int neoMaxConfirmations = 12;
        private const string TempConfirmError = "Something went wrong when confirming.\nThe transaction might have been succesful.\nCheck back later.";

        public void RequestConfirmation(string transactionHash, int confirmationCount, Action<string> callback)
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
                        if (confirmationCount <= neoMaxConfirmations)
                        {
                            callback("pending");
                        }
                        else
                        {
                            callback(TempConfirmError);
                        }
                    },
                    (response) =>
                    {
                        if (response.HasNode("vouts"))
                        {
                            callback(null);
                        }
                        else
                        {
                            if (confirmationCount <= neoMaxConfirmations)
                            {
                                callback("pending");
                            }
                            else
                            {
                                callback(TempConfirmError);
                            }
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
                                        Decimals = GetTokenDecimals(entry.symbol),
                                        Ids = entry.ids
                                    };
                                }

                                var stakedAmount = AmountFromString(acc.stake.amount, GetTokenDecimals("SOUL"));
                                var claimableAmount = AmountFromString(acc.stake.unclaimed, GetTokenDecimals("KCAL"));

                                var stakeTimestamp = new Timestamp(acc.stake.time);

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
                                        Log.WriteWarning(error);
                                    }


                                    var state = new AccountState()
                                    {
                                        platform = platform,
                                        address = acc.address,
                                        name = acc.name,
                                        balances = balanceMap.Values.ToArray(),
                                        flags = AccountFlags.None
                                    };

                                    if (stakedAmount >= SoulMasterStakeAmount)
                                    {
                                        state.flags |= AccountFlags.Master;
                                    }

                                    if (acc.validator.Equals("Primary") || acc.validator.Equals("Secondary"))
                                    {
                                        state.flags |= AccountFlags.Validator;
                                    }

                                    state.stakeTime = stakeTimestamp;

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
                                        Log.WriteWarning(error);
                                    }

                                    var state = new AccountState()
                                    {
                                        platform = platform,
                                        address = keys.Address,
                                        name = ValidationUtils.ANONYMOUS, // TODO support NNS
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

        public void BlankState()
        {
            var platforms = CurrentAccount.platforms.Split();

            _states.Clear();
            foreach (var platform in platforms)
            {
                _states[platform] = new AccountState()
                {
                    platform = platform,
                    address = GetAddress(CurrentIndex, platform),
                    balances = new Balance[0],
                    flags = AccountFlags.None,
                    name = ValidationUtils.ANONYMOUS,
                };
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

                Log.Write($"Found pending {platform} swap: {amount} {swap.symbol}");

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

            /*
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
            */

            this.Accounts = accounts.ToArray();
            SaveAccounts();
        }

        internal void DeleteAll()
        {
            this.Accounts = new Account[0];
        }

        public void RefreshTtrsNft(bool force, Action callback = null)
        {
            var now = DateTime.UtcNow;
            var diff = now - _lastTtrsNftRefresh;

            if (!force && diff.TotalSeconds < 30)
            {
                InvokeRefreshCallback();
                return;
            }

            _lastTtrsNftRefresh = now;
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
                            var cache = Cache.GetDataNode("tokens", Cache.FileType.JSON, 0, CurrentState.address);

                            if (cache == null)
                            {
                                cache = DataNode.CreateArray();
                            }

                            Log.Write("Getting NFTs...");
                            foreach (var balanceEntry in CurrentState.balances)
                            {
                                if (balanceEntry.Symbol == "TTRS")
                                {
                                    // Initializing NFT dictionary if needed.
                                    if (!_ttrsNft.ContainsKey(platform))
                                        _ttrsNft.Add(platform, new List<TokenData>());

                                    var idList = "";
                                    int loadedTokenCounter = 0;
                                    foreach (var id in balanceEntry.Ids)
                                    {
                                        if (String.IsNullOrEmpty(idList))
                                            idList += "\"" + id + "\"";
                                        else
                                            idList += ",\"" + id + "\"";

                                        // Checking if token is cached.
                                        DataNode token = null;
                                        foreach (var cachedToken in cache.Children)
                                        {
                                            if (cachedToken.GetString("id") == id)
                                            {
                                                token = cachedToken;
                                                break;
                                            }
                                        }

                                        if (token != null)
                                        {
                                            // Loading token from cache.
                                            var tokenId = token.GetString("id");

                                            loadedTokenCounter++;

                                            // Checking if token already loaded to dictionary.
                                            if (!_ttrsNft[platform].Exists(x => x.ID == tokenId))
                                            {
                                                var tokenData = new TokenData();

                                                tokenData.ID = tokenId;
                                                tokenData.chainName = token.GetString("chain-name");
                                                tokenData.ownerAddress = token.GetString("owner-address");
                                                tokenData.ram = token.GetString("ram");
                                                tokenData.rom = token.GetString("rom");
                                                tokenData.forSale = token.GetString("for-sale") == "true";

                                                _ttrsNft[platform].Add(tokenData);
                                            }

                                            if (loadedTokenCounter == balanceEntry.Ids.Length)
                                            {
                                                // We finished loading tokens.
                                                // Saving them in cache.
                                                Cache.AddDataNode("tokens", Cache.FileType.JSON, cache, CurrentState.address);

                                                ReportWalletTtrsNft(platform);
                                            }
                                        }
                                        else
                                        {
                                            StartCoroutine(phantasmaApi.GetTokenData(balanceEntry.Symbol, id, (tokenData) =>
                                            {
                                                loadedTokenCounter++;

                                                token = cache.AddNode(DataNode.CreateObject());
                                                token.AddField("id", tokenData.ID);
                                                token.AddField("chain-name", tokenData.chainName);
                                                token.AddField("owner-address", tokenData.ownerAddress);
                                                token.AddField("ram", tokenData.ram);
                                                token.AddField("rom", tokenData.rom);
                                                token.AddField("for-sale", tokenData.forSale);

                                                _ttrsNft[platform].Add(tokenData);

                                                if (loadedTokenCounter == balanceEntry.Ids.Length)
                                                {
                                                    // We finished loading tokens.
                                                    // Saving them in cache.
                                                    Cache.AddDataNode("tokens", Cache.FileType.JSON, cache, CurrentState.address);

                                                    ReportWalletTtrsNft(platform);
                                                }
                                            }, (error, msg) =>
                                            {
                                                Log.Write(msg);
                                            }));
                                        }
                                    }

                                    if (!String.IsNullOrEmpty(idList))
                                    {
                                        // Getting NFT descriptions.
                                        StartCoroutine(GoatiStore.LoadStoreNft(idList, (item) =>
                                        {
                                            StartCoroutine(GoatiStore.DownloadImage(item));
                                        }));
                                    }
                                }
                            }
                        }
                        break;

                    default:
                        ReportWalletTtrsNft(platform);
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
                            var keys = PhantasmaKeys.FromWIF(account.WIF);
                            StartCoroutine(phantasmaApi.GetAddressTransactions(keys.Address.Text, 1, 20, (x, page, max) =>
                            {
                                var history = new List<HistoryEntry>();

                                foreach (var tx in x.txs)
                                {
                                    history.Add(new HistoryEntry()
                                    {
                                        hash = tx.hash,
                                        date = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(tx.timestamp).ToLocalTime(),
                                        url = GetPhantasmaTransactionURL(tx.hash)
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
                                var alreadyAddedHashes = new List<string>(); // Neoscan sends some transactions twice, should filter them.

                                var history = new List<HistoryEntry>();

                                var entries = response.GetNode("entries");
                                foreach (var entry in entries.Children)
                                {
                                    var hash = entry.GetString("txid");
                                    if (alreadyAddedHashes.Contains(hash) == false)
                                    {
                                        var time = entry.GetUInt32("time");

                                        history.Add(new HistoryEntry()
                                        {
                                            hash = hash,
                                            date = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(time).ToLocalTime(),
                                            url = GetNeoscanTransactionURL(hash),
                                        });

                                        alreadyAddedHashes.Add(hash);
                                    }
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

        private string GetPhantasmaTransactionURL(string hash)
        {
            return $"https://explorer.phantasma.io/tx/{hash}";
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
            var url = Settings.neoscanURL;

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

        internal void SettleSwap(string sourcePlatform, string destPlatform, string pendingHash, Action<Hash, string> callback)
        {
            StartCoroutine(phantasmaApi.SettleSwap(sourcePlatform, destPlatform, pendingHash, (hash) =>
            {
                callback(Hash.Parse(hash), null);
            }, (error, msg) =>
            {
                Log.WriteWarning(msg);
                callback(Hash.Null, msg);
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

        internal void ReplaceAccountWIF(int currentIndex, string wif)
        {
            if (currentIndex < 0 || currentIndex >= Accounts.Length)
            {
                return;
            }

            Accounts[currentIndex].WIF = wif;
            SaveAccounts();
        }

        public bool RenameAccount(string newName)
        {
            foreach (var account in Accounts)
            {
                if (account.name.Equals(newName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            Accounts[CurrentIndex].name = newName;
            SaveAccounts();
            return true;
        }

        internal void ValidateAccountName(string name, Action<string> callback)
        {
            StartCoroutine(
                this.phantasmaApi.LookUpName(name, (address) =>
                {
                    callback(address);
                },
                (error, msg) =>
                {
                    callback(null);
                })
            );
        }

        public string GetAddress(int index, PlatformKind platform)
        {
            if (index < 0 || index >= Accounts.Length)
            {
                return null;
            }

            if (index == _selectedAccountIndex)
            {
                if (_states.ContainsKey(platform))
                {
                    return _states[platform].address;
                }
            }

            var wif = Accounts[index].WIF;
            switch (platform)
            {
                case PlatformKind.Phantasma:
                    return PhantasmaKeys.FromWIF(wif).Address.Text;

                case PlatformKind.Neo:
                    return NeoKeys.FromWIF(wif).Address;
            }


            return null;
        }
    }
}
