using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Linq;
using Phantasma.SDK;
using Poltergeist.Neo2.Core;
using LunarLabs.Parser;
using Archive = Phantasma.SDK.Archive;
using Poltergeist.PhantasmaLegacy.Cryptography;
using System.Numerics;
using Poltergeist.PhantasmaLegacy.Ethereum;
using Phantasma.Shared.Types;
using Phantasma.Core.Domain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Numerics;
using Phantasma.Shared.Utils;
using Phantasma.Shared;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography.ECDsa;

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
        Ethereum = 0x4,
        BSC = 0x8,
    }

    public struct Account
    {
        public string name;
        public PlatformKind platforms;
        public string phaAddress;
        public string neoAddress;
        public string ethAddress;
        public string WIF;
        public bool passwordProtected;
        public int passwordIterations;
        public string salt;
        public string iv;
        public string password; // Not used after account upgrade to version 2.
        public string misc;

        public override string ToString()
        {
            return $"{name.ToUpper()} [{platforms}]";
        }

        public string GetWif(string passwordHash)
        {
            return String.IsNullOrEmpty(passwordHash) ? WIF : AccountManager.DecryptString(WIF, passwordHash, iv);
        }
    }

    public struct AccountLegacyV1
    {
        public static readonly int MinPasswordLength = 6;
        public static readonly int MaxPasswordLength = 32;

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

    public struct AccountsExport
    {
        public string walletIdentifier;
        public int accountsVersion;
        public string accounts;
        public bool passwordProtected;
        public int passwordIterations;
        public string salt;
        public string iv;
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
            if (!token.IsSwappable())
            {
                return kind;
            }

            PlatformKind targets;

            switch (kind)
            {
                case PlatformKind.Phantasma:
                    targets = PlatformKind.Phantasma;
                    targets |= Tokens.HasSwappableToken(token.symbol, PlatformKind.Neo) ? PlatformKind.Neo : PlatformKind.None;
                    targets |= Tokens.HasSwappableToken(token.symbol, PlatformKind.Ethereum) ? PlatformKind.Ethereum : PlatformKind.None;
                    targets |= Tokens.HasSwappableToken(token.symbol, PlatformKind.BSC) ? PlatformKind.BSC : PlatformKind.None;
                    return targets;

                case PlatformKind.Neo:
                    targets = PlatformKind.Neo;
                    targets |= Tokens.HasSwappableToken(token.symbol, PlatformKind.Phantasma) ? PlatformKind.Phantasma : PlatformKind.None;
                    return targets;

                case PlatformKind.Ethereum:
                    targets = PlatformKind.Ethereum;
                    targets |= Tokens.HasSwappableToken(token.symbol, PlatformKind.Phantasma) ? PlatformKind.Phantasma : PlatformKind.None;
                    return targets;

                case PlatformKind.BSC:
                    targets = PlatformKind.BSC;
                    targets |= Tokens.HasSwappableToken(token.symbol, PlatformKind.Phantasma) ? PlatformKind.Phantasma : PlatformKind.None;
                    return targets;

                default:
                    return PlatformKind.None;
            }
        }
        public static bool ValidateTransferTarget(this PlatformKind kind, Token token, PlatformKind targetKind)
        {
            var targets = kind.GetTransferTargets(token);
            return targets.HasFlag(targetKind);
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

        public Archive[] archives;
        public string avatarData;
        public uint availableStorage;
        public uint usedStorage;
        public uint totalStorage => availableStorage + usedStorage;

        public Dictionary<string, string> dappTokens = new Dictionary<string, string>();

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

        public void RegisterDappToken(string dapp, string token)
        {
            dappTokens[dapp] = token;
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
        public bool Burnable;
        public bool Fungible;
        public string PendingPlatform;
        public string PendingHash;
        public string[] Ids;

        public decimal Total => Available + Staked + Pending + Claimable;
    }

    public class RefreshStatus
    {
        // Balance
        public bool BalanceRefreshing;
        public DateTime LastBalanceRefresh;
        public Action BalanceRefreshCallback;
        // History
        public bool HistoryRefreshing;
        public DateTime LastHistoryRefresh;

        public override string ToString()
        {
            return $"BalanceRefreshing: {BalanceRefreshing}, LastBalanceRefresh: {LastBalanceRefresh}, HistoryRefreshing: {HistoryRefreshing}, LastHistoryRefresh: {LastHistoryRefresh}";
        }
    }

    public class AccountManager : MonoBehaviour
    {
        public static readonly int MinPasswordLength = 6;
        public static readonly int MaxPasswordLength = 32;
        public static readonly int MinAccountNameLength = 3;
        public static readonly int MaxAccountNameLength = 16;
        public string WalletIdentifier => "PGT" + UnityEngine.Application.version;

        public Settings Settings { get; private set; }

        public List<Account> Accounts { get; private set; }
        public bool AccountsAreReadyToBeUsed = false;

        private Dictionary<string, decimal> _tokenPrices = new Dictionary<string, decimal>();
        public string CurrentTokenCurrency { get; private set; }

        private int _selectedAccountIndex;
        public int CurrentIndex => _selectedAccountIndex;
        public Account CurrentAccount => HasSelection ? Accounts[_selectedAccountIndex] : new Account() { };
        public string CurrentPasswordHash;
        public string CurrentWif => Accounts[_selectedAccountIndex].GetWif(CurrentPasswordHash);

        public bool HasSelection => _selectedAccountIndex >= 0 && _selectedAccountIndex < Accounts.Count();

        private Dictionary<PlatformKind, AccountState> _states = new Dictionary<PlatformKind, AccountState>();
        private Dictionary<PlatformKind, List<TokenData>> _nfts = new Dictionary<PlatformKind, List<TokenData>>();
        private Dictionary<PlatformKind, HistoryEntry[]> _history = new Dictionary<PlatformKind, HistoryEntry[]>();
        public Dictionary<PlatformKind, RefreshStatus> _refreshStatus = new Dictionary<PlatformKind, RefreshStatus>();

        public PlatformKind CurrentPlatform { get; set; }
        public AccountState CurrentState => _states.ContainsKey(CurrentPlatform) ? _states[CurrentPlatform] : null;
        public List<TokenData> CurrentNfts => _nfts.ContainsKey(CurrentPlatform) ? _nfts[CurrentPlatform] : null;
        public HistoryEntry[] CurrentHistory => _history.ContainsKey(CurrentPlatform) ? _history[CurrentPlatform] : null;

        public AccountState MainState => _states.ContainsKey(PlatformKind.Phantasma) ? _states[PlatformKind.Phantasma] : null;

        private bool nftDescriptionsAreFullyLoaded;
        private TtrsNftSortMode currentTtrsNftsSortMode = TtrsNftSortMode.None;
        private NftSortMode currentNftsSortMode = NftSortMode.None;
        private SortDirection currentNftsSortDirection = SortDirection.None;

        public static AccountManager Instance { get; private set; }

        public string Status { get; private set; }
        public bool Ready => Status == "ok";
        public bool BalanceRefreshing => _refreshStatus.ContainsKey(CurrentPlatform) ? _refreshStatus[CurrentPlatform].BalanceRefreshing : false;
        public bool HistoryRefreshing => _refreshStatus.ContainsKey(CurrentPlatform) ? _refreshStatus[CurrentPlatform].HistoryRefreshing : false;

        public Phantasma.SDK.PhantasmaAPI phantasmaApi { get; private set; }
        public Phantasma.SDK.EthereumAPI ethereumApi { get; private set; }
        public Phantasma.SDK.EthereumAPI binanceSmartChainApi { get; private set; }
        public Poltergeist.Neo2.Core.NeoAPI neoApi;

        public static PlatformKind[] AvailablePlatforms { get; private set; }
        public static PlatformKind MergeAvailablePlatforms()
        {
            var platforms = PlatformKind.None;
            foreach (var platform in AccountManager.AvailablePlatforms)
            {
                platforms |= platform;
            }
            return platforms;
        }

        private Dictionary<string, string> _currencyMap = new Dictionary<string, string>();
        public IEnumerable<string> Currencies => _currencyMap.Keys;

        public static readonly int SoulMasterStakeAmount = 50000;

        private DateTime _lastPriceUpdate = DateTime.MinValue;

        private bool _accountInitialized;

        private string etherscanAPIToken;

        private void Awake()
        {
            Instance = this;
            Settings = new Settings();

            Status = "Initializing wallet...";

            _currencyMap["AUD"] = "A$";
            _currencyMap["CAD"] = "C$";
            _currencyMap["EUR"] = "€";
            _currencyMap["GBP"] = "\u00A3";
            _currencyMap["RUB"] = "\u20BD";
            _currencyMap["USD"] = "$";
            _currencyMap["JPY"] = "¥";

            var ethereumAPIKeys = Resources.Load<TextAsset>("ethereum_api");
            if (ethereumAPIKeys != null)
            {
                var lines = ethereumAPIKeys.text.Split('\n');
                if (lines.Length > 0)
                {
                    etherscanAPIToken = lines[0].Trim();
                }
            }
            if (string.IsNullOrEmpty(etherscanAPIToken))
            {
                Log.WriteWarning("No Etherscan API key found, Ethereum balances wont work!");
            }

            var platforms = new List<PlatformKind>();
            platforms.Add(PlatformKind.Phantasma);
            platforms.Add(PlatformKind.Neo);
            platforms.Add(PlatformKind.Ethereum);
            platforms.Add(PlatformKind.BSC);

            AvailablePlatforms = platforms.ToArray();
        }

        public string GetTokenWorth(string symbol, decimal amount)
        {
            bool hasLocalCurrency = !string.IsNullOrEmpty(CurrentTokenCurrency) && _currencyMap.ContainsKey(CurrentTokenCurrency);
            if (_tokenPrices.ContainsKey(symbol) && hasLocalCurrency)
            {
                var price = _tokenPrices[symbol] * amount;
                var ch = _currencyMap[CurrentTokenCurrency];
                return $"{WalletGUI.MoneyFormat(price, MoneyFormatType.Short)} {ch}";
            }
            else
            {
                return null;
            }
        }

        private IEnumerator FetchTokenPrices(IEnumerable<Token> symbols, string currency)
        {
            var separator = "%2C";
            var url = "https://api.coingecko.com/api/v3/simple/price?ids=" + string.Join(separator, symbols.Where(x => !String.IsNullOrEmpty(x.apiSymbol)).Select(x => x.apiSymbol).Distinct().ToList()) + "&vs_currencies=" + currency;
            return WebClient.RESTRequest(url, WebClient.DefaultTimeout, (error, msg) =>
            {

            },
            (response) =>
            {
                try
                {
                    foreach (var symbol in symbols)
                    {
                        var node = response.GetNode(symbol.apiSymbol);
                        if (node != null)
                        {
                            var price = node.GetDecimal(currency);

                            SetTokenPrice(symbol.symbol, price);
                        }
                        else
                        {
                            Log.Write($"Cannot get price for '{symbol.apiSymbol}'.");
                        }
                    }

                    // GOATI token price is pegged to 0.1$.
                    SetTokenPrice("GOATI", Convert.ToDecimal(0.1));
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

        public const string WalletVersionTag = "wallet.list.version";
        public const string WalletTag = "wallet.list";
        // TODO: Remove before release.
        public const string WalletLegacyTag = "wallet.list.legacy";

        private int rpcNumberPhantasma; // Total number of Phantasma RPCs, received from getpeers.json.
        private int rpcNumberNeo; // Total number of Neo RPCs.
        private int rpcNumberBsc; // Total number of Bsc RPCs.
        private int rpcBenchmarkedPhantasma; // Number of Phantasma RPCs which speed already measured.
        private int rpcBenchmarkedNeo; // Number of Neo RPCs which speed already measured.
        private int rpcBenchmarkedBsc; // Number of Bsc RPCs which speed already measured.
        public int rpcAvailablePhantasma = 0;
        public int rpcAvailableNeo = 0;
        public int rpcAvailableBsc = 0;
        private class RpcBenchmarkData
        {
            public string Url;
            public bool ConnectionError;
            public TimeSpan ResponseTime;

            public RpcBenchmarkData(string url, bool connectionError, TimeSpan responseTime)
            {
                Url = url;
                ConnectionError = connectionError;
                ResponseTime = responseTime;
            }
        }
        private List<RpcBenchmarkData> rpcResponseTimesPhantasma = new List<RpcBenchmarkData>();
        private List<RpcBenchmarkData> rpcResponseTimesNeo = new List<RpcBenchmarkData>();
        private List<RpcBenchmarkData> rpcResponseTimesBsc = new List<RpcBenchmarkData>();

        private string GetFastestWorkingRPCURL(PlatformKind platformKind, out TimeSpan responseTime)
        {
            string fastestRpcUrl = null;

            List<RpcBenchmarkData> platformRpcs = null;
            if (platformKind == PlatformKind.Phantasma)
                platformRpcs = rpcResponseTimesPhantasma;
            else if (platformKind == PlatformKind.Neo)
                platformRpcs = rpcResponseTimesNeo;
            else if (platformKind == PlatformKind.BSC)
                platformRpcs = rpcResponseTimesBsc;

            responseTime = TimeSpan.Zero;

            foreach (var rpcResponseTime in platformRpcs)
            {
                if (!rpcResponseTime.ConnectionError && String.IsNullOrEmpty(fastestRpcUrl))
                {
                    // At first just initializing with first working RPC.
                    fastestRpcUrl = rpcResponseTime.Url;
                    responseTime = rpcResponseTime.ResponseTime;
                }
                else if (!rpcResponseTime.ConnectionError && rpcResponseTime.ResponseTime < responseTime)
                {
                    // Faster RPC found, switching.
                    fastestRpcUrl = rpcResponseTime.Url;
                    responseTime = rpcResponseTime.ResponseTime;
                }
            }
            return fastestRpcUrl;
        }

        public void UpdateRPCURL(PlatformKind platformKind)
        {
            if (Settings.nexusKind != NexusKind.Main_Net && Settings.nexusKind != NexusKind.Test_Net ||
                (platformKind == PlatformKind.Neo && Settings.nexusKind != NexusKind.Main_Net))
            {
                rpcAvailablePhantasma = 1;
                rpcAvailableNeo = 1;
                rpcAvailableBsc = 1;
                return; // No need to change RPC, it is set by custom settings.
            }

            if (platformKind == PlatformKind.Phantasma)
            {
                string url;
                if(Settings.nexusKind == NexusKind.Main_Net)
                {
                    url = $"https://peers.phantasma.io/mainnet-getpeers.json";
                }
                else
                {
                    url = $"https://peers.phantasma.io/testnet-getpeers.json";
                }

                rpcBenchmarkedPhantasma = 0;
                rpcResponseTimesPhantasma = new List<RpcBenchmarkData>();

                StartCoroutine(
                    WebClient.RESTRequest(url, WebClient.DefaultTimeout, (error, msg) =>
                    {
                        Log.Write("auto error => " + error);
                    },
                    (response) =>
                    {
                        if (response != null)
                        {
                            rpcNumberPhantasma = response.ChildCount;

                            if (String.IsNullOrEmpty(Settings.phantasmaRPCURL))
                            {
                                // Checking if we are still on mainnet
                                if (Settings.nexusKind == NexusKind.Main_Net)
                                {
                                    // If we have no previously used RPC, we select random one at first.
                                    var index = ((int)(Time.realtimeSinceStartup * 1000)) % rpcNumberPhantasma;
                                    var node = response.GetNodeByIndex(index);
                                    var result = node.GetString("url") + "/rpc";
                                    Settings.phantasmaRPCURL = result;
                                    Log.Write($"Changed Phantasma RPC url {index} => {result}");
                                }
                            }

                            UpdateAPIs();

                            // Benchmarking RPCs.
                            foreach (var node in response.Children)
                            {
                                var rpcUrl = node.GetString("url") + "/rpc";

                                StartCoroutine(
                                    WebClient.Ping(rpcUrl, (error, msg) =>
                                    {
                                        Log.Write("Ping error: " + error);

                                        rpcBenchmarkedPhantasma++;

                                        lock (rpcResponseTimesPhantasma)
                                        {
                                            rpcResponseTimesPhantasma.Add(new RpcBenchmarkData(rpcUrl, true, new TimeSpan()));
                                        }

                                        if (rpcBenchmarkedPhantasma == rpcNumberPhantasma)
                                        {
                                        // We finished benchmarking, time to select best RPC server.
                                        TimeSpan bestTime;
                                            string bestRpcUrl = GetFastestWorkingRPCURL(PlatformKind.Phantasma, out bestTime);

                                            if (String.IsNullOrEmpty(bestRpcUrl))
                                            {
                                                Log.WriteWarning("All Phantasma RPC servers are unavailable. Please check your network connection.");
                                            }
                                            else
                                            {
                                                Log.Write($"Fastest Phantasma RPC is {bestRpcUrl}: {new DateTime(bestTime.Ticks).ToString("ss.fff")} sec.");

                                            // Checking if we are still on mainnet
                                            if (Settings.nexusKind == NexusKind.Main_Net)
                                                {
                                                    Settings.phantasmaRPCURL = bestRpcUrl;
                                                    UpdateAPIs();
                                                    Settings.SaveOnExit();
                                                }
                                            }
                                        }
                                    },
                                    (responseTime) =>
                                    {
                                        rpcBenchmarkedPhantasma++;

                                        rpcAvailablePhantasma++;

                                        lock (rpcResponseTimesPhantasma)
                                        {
                                            rpcResponseTimesPhantasma.Add(new RpcBenchmarkData(rpcUrl, false, responseTime));
                                        }

                                        if (rpcBenchmarkedPhantasma == rpcNumberPhantasma)
                                        {
                                        // We finished benchmarking, time to select best RPC server.
                                        TimeSpan bestTime;
                                            string bestRpcUrl = GetFastestWorkingRPCURL(PlatformKind.Phantasma, out bestTime);

                                            if (String.IsNullOrEmpty(bestRpcUrl))
                                            {
                                                Log.WriteWarning("All Phantasma RPC servers are unavailable. Please check your network connection.");
                                            }
                                            else
                                            {
                                            // Checking if we are still on mainnet
                                            if (Settings.nexusKind == NexusKind.Main_Net)
                                                {
                                                    Log.Write($"Fastest Phantasma RPC is {bestRpcUrl}: {new DateTime(bestTime.Ticks).ToString("ss.fff")} sec.");
                                                    Settings.phantasmaRPCURL = bestRpcUrl;
                                                    UpdateAPIs();
                                                    Settings.SaveOnExit();
                                                }
                                            }
                                        }
                                    })
                                );
                            }
                        }
                    })
                );
            }
            else if (platformKind == PlatformKind.Neo)
            {
                rpcBenchmarkedNeo = 0;
                rpcResponseTimesNeo = new List<RpcBenchmarkData>();

                var neoRpcList = Poltergeist.Neo2.Utils.NeoRpcs.GetList();
                rpcNumberNeo = neoRpcList.Count;

                if (String.IsNullOrEmpty(Settings.neoRPCURL))
                {
                    // Checking if we are still on mainnet
                    if (Settings.nexusKind == NexusKind.Main_Net)
                    {
                        // If we have no previously used RPC, we select random one at first.
                        var index = ((int)(Time.realtimeSinceStartup * 1000)) % rpcNumberNeo;
                        var result = neoRpcList[index];
                        Settings.neoRPCURL = result;
                        Log.Write($"Changed Neo RPC url {index} => {result}");
                    }
                }

                UpdateAPIs();

                // Benchmarking RPCs.
                foreach (var rpcUrl in neoRpcList)
                {
                    StartCoroutine(
                        WebClient.Ping(rpcUrl, (error, msg) =>
                        {
                            Log.Write("Ping error: " + error);

                            rpcBenchmarkedNeo++;

                            lock (rpcResponseTimesNeo)
                            {
                                rpcResponseTimesNeo.Add(new RpcBenchmarkData(rpcUrl, true, new TimeSpan()));
                            }

                            if (rpcBenchmarkedNeo == rpcNumberNeo)
                            {
                                // We finished benchmarking, time to select best RPC server.
                                TimeSpan bestTime;
                                string bestRpcUrl = GetFastestWorkingRPCURL(PlatformKind.Neo, out bestTime);

                                if (String.IsNullOrEmpty(bestRpcUrl))
                                {
                                    Log.WriteWarning("All Neo RPC servers are unavailable. Please check your network connection.");
                                }
                                else
                                {
                                    // Checking if we are still on mainnet
                                    if (Settings.nexusKind == NexusKind.Main_Net)
                                    {
                                        Log.Write($"Fastest Neo RPC is {bestRpcUrl}: {new DateTime(bestTime.Ticks).ToString("ss.fff")} sec.");
                                        Settings.neoRPCURL = bestRpcUrl;
                                        UpdateAPIs();
                                        Settings.SaveOnExit();
                                    }
                                }
                            }
                        },
                        (responseTime) =>
                        {
                            rpcBenchmarkedNeo++;

                            rpcAvailableNeo++;

                            lock (rpcResponseTimesNeo)
                            {
                                rpcResponseTimesNeo.Add(new RpcBenchmarkData(rpcUrl, false, responseTime));
                            }

                            if (rpcBenchmarkedNeo == rpcNumberNeo)
                            {
                                // We finished benchmarking, time to select best RPC server.
                                TimeSpan bestTime;
                                string bestRpcUrl = GetFastestWorkingRPCURL(PlatformKind.Neo, out bestTime);

                                if (String.IsNullOrEmpty(bestRpcUrl))
                                {
                                    Log.WriteWarning("All Neo RPC servers are unavailable. Please check your network connection.");
                                }
                                else
                                {
                                    // Checking if we are still on mainnet
                                    if (Settings.nexusKind == NexusKind.Main_Net)
                                    {
                                        Log.Write($"Fastest Neo RPC is {bestRpcUrl}: {new DateTime(bestTime.Ticks).ToString("ss.fff")} sec.");
                                        Settings.neoRPCURL = bestRpcUrl;
                                        UpdateAPIs();
                                        Settings.SaveOnExit();
                                    }
                                }
                            }
                        })
                    );
                }
            }
            else if (platformKind == PlatformKind.BSC)
            {
                rpcBenchmarkedBsc = 0;
                rpcResponseTimesBsc = new List<RpcBenchmarkData>();

                var bscRpcList = Phantasma.Bsc.Utils.BscRpcs.GetList(Settings.nexusKind == NexusKind.Main_Net);
                rpcNumberBsc = bscRpcList.Count;

                if (String.IsNullOrEmpty(Settings.binanceSmartChainRPCURL))
                {
                    // If we have no previously used RPC, we select random one at first.
                    var index = ((int)(Time.realtimeSinceStartup * 1000)) % rpcNumberBsc;
                    var result = bscRpcList[index];
                    Settings.binanceSmartChainRPCURL = result;
                    Log.Write($"Changed BSC RPC url {index} => {result}");
                }

                UpdateAPIs();

                // Benchmarking RPCs.
                foreach (var rpcUrl in bscRpcList)
                {
                    StartCoroutine(
                        WebClient.Ping(rpcUrl, (error, msg) =>
                        {
                            Log.Write("Ping error: " + error);

                            rpcBenchmarkedBsc++;

                            lock (rpcResponseTimesBsc)
                            {
                                rpcResponseTimesBsc.Add(new RpcBenchmarkData(rpcUrl, true, new TimeSpan()));
                            }

                            if (rpcBenchmarkedBsc == rpcNumberBsc)
                            {
                                // We finished benchmarking, time to select best RPC server.
                                TimeSpan bestTime;
                                string bestRpcUrl = GetFastestWorkingRPCURL(PlatformKind.BSC, out bestTime);

                                if (String.IsNullOrEmpty(bestRpcUrl))
                                {
                                    Log.WriteWarning("All BSC RPC servers are unavailable. Please check your network connection.");
                                }
                                else
                                {
                                    Log.Write($"Fastest BSC RPC is {bestRpcUrl}: {new DateTime(bestTime.Ticks).ToString("ss.fff")} sec.");
                                    Settings.binanceSmartChainRPCURL = bestRpcUrl;
                                    UpdateAPIs();
                                    Settings.SaveOnExit();
                                }
                            }
                        },
                        (responseTime) =>
                        {
                            rpcBenchmarkedBsc++;

                            rpcAvailableBsc++;

                            lock (rpcResponseTimesBsc)
                            {
                                rpcResponseTimesBsc.Add(new RpcBenchmarkData(rpcUrl, false, responseTime));
                            }

                            if (rpcBenchmarkedBsc == rpcNumberBsc)
                            {
                                // We finished benchmarking, time to select best RPC server.
                                TimeSpan bestTime;
                                string bestRpcUrl = GetFastestWorkingRPCURL(PlatformKind.BSC, out bestTime);

                                if (String.IsNullOrEmpty(bestRpcUrl))
                                {
                                    Log.WriteWarning("All BSC RPC servers are unavailable. Please check your network connection.");
                                }
                                else
                                {
                                    Log.Write($"Fastest BSC RPC is {bestRpcUrl}: {new DateTime(bestTime.Ticks).ToString("ss.fff")} sec.");
                                    Settings.binanceSmartChainRPCURL = bestRpcUrl;
                                    UpdateAPIs();
                                    Settings.SaveOnExit();
                                }
                            }
                        })
                    );
                }
            }
        }

        public void ChangeFaultyRPCURL(PlatformKind platformKind)
        {
            if (Settings.nexusKind != NexusKind.Main_Net ||
                (platformKind == PlatformKind.BSC && Settings.nexusKind != NexusKind.Main_Net && Settings.nexusKind != NexusKind.Test_Net))
            {
                return; // Fallback works only for mainnet or BSC testnet.
            }

            if (platformKind == PlatformKind.Phantasma)
            {
                Log.Write($"Changing faulty Phantasma RPC {Settings.phantasmaRPCURL}.");

                // Now we have one less working RPC.
                if(rpcAvailablePhantasma > 0)
                    rpcAvailablePhantasma--;

                // Marking faulty RPC.
                var currentRpc = rpcResponseTimesPhantasma.Find(x => x.Url == Settings.phantasmaRPCURL);
                if (currentRpc != null)
                    currentRpc.ConnectionError = true;

                // Switching to working RPC.
                TimeSpan bestTime;
                string bestRpcUrl = GetFastestWorkingRPCURL(platformKind, out bestTime);

                if (String.IsNullOrEmpty(bestRpcUrl))
                {
                    Log.WriteWarning("All Phantasma RPC servers are unavailable. Please check your network connection.");
                }
                else
                {
                    Log.Write($"Next fastest Phantasma RPC is {bestRpcUrl}: {new DateTime(bestTime.Ticks).ToString("ss.fff")} sec.");
                    Settings.phantasmaRPCURL = bestRpcUrl;
                    UpdateAPIs();
                }
            }
            else if (platformKind == PlatformKind.Neo)
            {
                // TODO: This code is not used yet, ChangeFaultyRPCURL() not called on Neo connection errors.

                Log.Write($"Changing faulty Neo RPC {Settings.neoRPCURL}.");

                // Now we have one less working RPC.
                if (rpcAvailableNeo > 0)
                    rpcAvailableNeo--;

                // Marking faulty RPC.
                var currentRpc = rpcResponseTimesNeo.Find(x => x.Url == Settings.neoRPCURL);
                if (currentRpc != null)
                    currentRpc.ConnectionError = true;

                // Switching to working RPC.
                TimeSpan bestTime;
                string bestRpcUrl = GetFastestWorkingRPCURL(platformKind, out bestTime);

                if (String.IsNullOrEmpty(bestRpcUrl))
                {
                    Log.WriteWarning("All Neo RPC servers are unavailable. Please check your network connection.");
                }
                else
                {
                    Log.Write($"Next fastest Neo RPC is {bestRpcUrl}: {new DateTime(bestTime.Ticks).ToString("ss.fff")} sec.");
                    Settings.neoRPCURL = bestRpcUrl;
                    UpdateAPIs();
                }
            }
            else if (platformKind == PlatformKind.BSC)
            {
                Log.Write($"Changing faulty BSC RPC {Settings.binanceSmartChainRPCURL}.");

                // Now we have one less working RPC.
                if (rpcAvailableBsc > 0)
                    rpcAvailableBsc--;

                // Marking faulty RPC.
                var currentRpc = rpcResponseTimesBsc.Find(x => x.Url == Settings.binanceSmartChainRPCURL);
                if (currentRpc != null)
                    currentRpc.ConnectionError = true;

                // Switching to working RPC.
                TimeSpan bestTime;
                string bestRpcUrl = GetFastestWorkingRPCURL(platformKind, out bestTime);

                if (String.IsNullOrEmpty(bestRpcUrl))
                {
                    Log.WriteWarning("All BSC RPC servers are unavailable. Please check your network connection.");
                }
                else
                {
                    Log.Write($"Next fastest BSC RPC is {bestRpcUrl}: {new DateTime(bestTime.Ticks).ToString("ss.fff")} sec.");
                    Settings.binanceSmartChainRPCURL = bestRpcUrl;
                    UpdateAPIs();
                }
            }
        }
        public static readonly int PasswordIterations = 100000;
        private static readonly int PasswordSaltByteSize = 64;
        private static readonly int PasswordHashByteSize = 32;
        public static void GetPasswordHash(string password, int passwordIterations, out string salt, out string passwordHash)
        {
            BouncyCastleHashing hashing = new BouncyCastleHashing();
            salt = Convert.ToBase64String(hashing.CreateSalt(PasswordSaltByteSize));
            passwordHash = hashing.PBKDF2_SHA256_GetHash(password, salt, passwordIterations, PasswordHashByteSize);
        }
        public static void GetPasswordHashBySalt(string password, int passwordIterations, string salt, out string passwordHash)
        {
            BouncyCastleHashing hashing = new BouncyCastleHashing();
            passwordHash = hashing.PBKDF2_SHA256_GetHash(password, salt, passwordIterations, PasswordHashByteSize);
        }
        public static string EncryptString(string stringToEncrypt, string key, out string iv)
        {
            var ivBytes = new byte[16];

            //Set up
            var keyParam = new Org.BouncyCastle.Crypto.Parameters.KeyParameter(Convert.FromBase64String(key));

            var secRandom = new Org.BouncyCastle.Security.SecureRandom();
            secRandom.NextBytes(ivBytes);

            var keyParamWithIV = new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(keyParam, ivBytes, 0, 16);

            var engine = new Org.BouncyCastle.Crypto.Engines.AesEngine();
            var blockCipher = new Org.BouncyCastle.Crypto.Modes.CbcBlockCipher(engine); //CBC
            var cipher = new Org.BouncyCastle.Crypto.Paddings.PaddedBufferedBlockCipher(blockCipher); //Default scheme is PKCS5/PKCS7

            // Encrypt
            cipher.Init(true, keyParamWithIV);
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(stringToEncrypt);
            var outputBytes = new byte[cipher.GetOutputSize(inputBytes.Length)];
            var length = cipher.ProcessBytes(inputBytes, outputBytes, 0);
            cipher.DoFinal(outputBytes, length); //Do the final block

            iv = Convert.ToBase64String(ivBytes);
            return Convert.ToBase64String(outputBytes);
        }
        public static string DecryptString(string stringToDecrypt, string key, string iv)
        {
            //Set up
            var keyParam = new Org.BouncyCastle.Crypto.Parameters.KeyParameter(Convert.FromBase64String(key));
            var ivBytes = Convert.FromBase64String(iv);
            var keyParamWithIV = new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(keyParam, ivBytes, 0, 16);

            var engine = new Org.BouncyCastle.Crypto.Engines.AesEngine();
            var blockCipher = new Org.BouncyCastle.Crypto.Modes.CbcBlockCipher(engine); //CBC
            var cipher = new Org.BouncyCastle.Crypto.Paddings.PaddedBufferedBlockCipher(blockCipher);

            cipher.Init(false, keyParamWithIV);
            var inputBytes = Convert.FromBase64String(stringToDecrypt);
            var resultExtraSize = new byte[cipher.GetOutputSize(inputBytes.Length)];
            var length = cipher.ProcessBytes(inputBytes, resultExtraSize, 0);
            length += cipher.DoFinal(resultExtraSize, length); //Do the final block

            var result = new byte[length];
            Array.Copy(resultExtraSize, result, length);

            return System.Text.Encoding.UTF8.GetString(result);
        }

        // Start is called before the first frame update
        void Start()
        {
            Settings.Load();

            UpdateRPCURL(PlatformKind.Phantasma);
            UpdateRPCURL(PlatformKind.Neo);
            UpdateRPCURL(PlatformKind.BSC);

            LoadNexus();

            // Version 1 - original account version used in PG up to version 1.9.
            // Version 2 - new account version.
            var walletVersion = PlayerPrefs.GetInt(WalletVersionTag, 1);

            var wallets = PlayerPrefs.GetString(WalletTag, "");

            Accounts = new List<Account>();

            if (walletVersion == 1 && !string.IsNullOrEmpty(wallets))
            {
                // TODO: Remove before release.
                // Saving old accounts for now.
                PlayerPrefs.SetString(WalletLegacyTag, wallets);

                // Legacy format, should be converted.
                var bytes = Base16.Decode(wallets);
                var accountsLegacy = Serialization.Unserialize<AccountLegacyV1[]>(bytes);

                foreach (var account in accountsLegacy)
                {
                    Accounts.Add(new Account
                    {
                        name = account.name,
                        platforms = account.platforms,
                        WIF = account.WIF,
                        password = account.password,
                        misc = account.misc
                    });
                }

                // Upgrading accounts.
                for (var i = 0; i < Accounts.Count(); i++)
                {
                    Log.Write($"Account {Accounts[i].name} version: {walletVersion}, will be upgraded");

                    var account = Accounts[i];

                    // Initializing public addresses.
                    var phaKeys = PhantasmaKeys.FromWIF(account.WIF);
                    account.phaAddress = phaKeys.Address.ToString();

                    var neoKeys = NeoKeys.FromWIF(account.WIF);
                    account.neoAddress = neoKeys.Address.ToString();

                    var ethereumAddressUtil = new Poltergeist.PhantasmaLegacy.Ethereum.Util.AddressUtil();
                    account.ethAddress = ethereumAddressUtil.ConvertToChecksumAddress(EthereumKey.FromWIF(account.WIF).Address);

                    if (!String.IsNullOrEmpty(Accounts[i].password))
                    {
                        account.passwordProtected = true;
                        account.passwordIterations = PasswordIterations;

                        // Encrypting WIF.
                        GetPasswordHash(account.password, account.passwordIterations, out string salt, out string passwordHash);
                        account.password = "";
                        account.salt = salt;

                        account.WIF = EncryptString(account.WIF, passwordHash, out string iv);
                        account.iv = iv;

                        // Decrypting to ensure there are no exceptions.
                        DecryptString(account.WIF, passwordHash, account.iv);
                    }
                    else
                    {
                        account.passwordProtected = false;
                    }

                    Accounts[i] = account;
                }

                SaveAccounts();
            }
            else if (!string.IsNullOrEmpty(wallets))
            {
                var bytes = Base16.Decode(wallets);
                Accounts = Serialization.Unserialize<Account[]>(bytes).ToList();
            }

            if (walletVersion == 2)
            {
                // Legacy seeds, we should mark accounts.
                for (var i = 0; i < Accounts.Count; i++)
                {
                    var account = Accounts[i];
                    account.misc = "legacy-seed";
                    Accounts[i] = account;
                }

                SaveAccounts();
            }

            AccountsAreReadyToBeUsed = true;
        }

        public void SaveAccounts()
        {
            PlayerPrefs.SetInt(WalletVersionTag, 3);

            var bytes = Serialization.Serialize(Accounts.ToArray());
            PlayerPrefs.SetString(WalletTag, Base16.Encode(bytes));
            PlayerPrefs.Save();
        }

        private IEnumerator GetTokens(Action<Token[]> callback)
        {
            while (Status != "ok")
            {
                var coroutine = StartCoroutine(phantasmaApi.GetTokens((tokens) =>
                {
                    callback(tokens);
                }, (error, msg) =>
                {
                    if (rpcAvailablePhantasma > 0 && Settings.nexusKind == NexusKind.Main_Net)
                    {
                        ChangeFaultyRPCURL(PlatformKind.Phantasma);
                    }
                    else
                    {
                        CurrentTokenCurrency = "";

                        Status = "ok"; // We are launching with uninitialized tokens,
                                       // to allow user to edit settings.
                        
                        Log.WriteWarning("Error: Launching with uninitialized tokens.");
                    }

                    Log.WriteWarning("Tokens initialization error: " + msg);
                }));

                yield return coroutine;
            }
        }

        private void TokensReinit()
        {

            StartCoroutine(GetTokens((tokens) =>
            {
                Tokens.Init(tokens);

                CurrentTokenCurrency = "";

                Status = "ok";

                StartCoroutine(phantasmaApi.GetPlatforms((platforms) =>
                {
                    foreach (var entry in platforms)
                    {
                        string interopAddress = entry.interop[0].external;
                        Log.Write($"{entry.platform} interop address: {interopAddress}");
                    }
                }, (error, msg) =>
                {
                    if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                    {
                        ChangeFaultyRPCURL(PlatformKind.Phantasma);
                    }
                    Log.Write("Cannot get platforms for interop addresses logging");
                }));
            }));
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

                StartCoroutine(FetchTokenPrices(Tokens.GetTokensForCoingecko(), CurrentTokenCurrency));
            }
        }

        public void UpdateAPIs(bool possibleNexusChange = false)
        {
            Log.Write("reinit APIs => " + Settings.phantasmaRPCURL);
            phantasmaApi = new PhantasmaAPI(Settings.phantasmaRPCURL);
            ethereumApi = new EthereumAPI(Settings.ethereumRPCURL);
            binanceSmartChainApi = new EthereumAPI(Settings.binanceSmartChainRPCURL);
            neoApi = new NeoAPI(Settings.neoRPCURL, Settings.neoscanURL);

            if (possibleNexusChange)
            {
                // We should renew all interop addresses when switching between nets.
                // Otherwise we might send funds to wrong interop address.
                ClearInteropMap();

                TokensReinit();
            }
        }

        private void LoadNexus()
        {
            UpdateAPIs(true);

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

        public decimal AmountFromString(string str, int decimals)
        {
            if (string.IsNullOrEmpty(str))
            {
                return 0;
            }

            var n = BigInteger.Parse(str);
            return UnitConversion.ToDecimal(n, decimals);
        }

        public void SignAndSendTransaction(string chain, byte[] script, BigInteger phaGasPrice, BigInteger phaGasLimit, byte[] payload, ProofOfWork PoW, IKeyPair customKeys, Action<Hash, string> callback, Func<byte[], byte[], byte[], byte[]> customSignFunction = null)
        {
            if (payload == null)
            {
                payload = System.Text.Encoding.UTF8.GetBytes(WalletIdentifier);
            }

            switch (CurrentPlatform)
            {
                case PlatformKind.Phantasma:
                    {
                        StartCoroutine(phantasmaApi.SignAndSendTransactionWithPayload(PhantasmaKeys.FromWIF(CurrentWif), customKeys, Settings.nexusName, script, chain, phaGasPrice, phaGasLimit, payload, PoW, (hashText) =>
                        {
                            var hash = Hash.Parse(hashText);
                            callback(hash, null);
                        }, (error, msg) =>
                        {
                            if(error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                            {
                                ChangeFaultyRPCURL(PlatformKind.Phantasma);
                            }
                            callback(Hash.Null, msg);
                        }, customSignFunction));
                        break;
                    }

                case PlatformKind.Neo:
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

                                            StartCoroutine(neoApi.GetUnspent(keys.Address,
                                            (unspent) =>
                                            {
                                                Log.Write("Got unspents for " + keys.Address);

                                                if (transfer.symbol == "NEO" || transfer.symbol == "GAS")
                                                {
                                                    CoroutineUtils.StartThrowingCoroutine(this, neoApi.SendAsset((tx, error) =>
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
                                                    },
                                                    (error, msg) =>
                                                    {
                                                        if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                                                        {
                                                            ChangeFaultyRPCURL(PlatformKind.Neo);
                                                        }
                                                        callback(Hash.Null, msg);
                                                    }, unspent, keys, transfer.destination, transfer.symbol, transfer.amount, transfer.interop, Settings.neoGasFee), ex => {
                                                        if (ex != null)
                                                        {
                                                            callback(Hash.Null, ex.ToString());
                                                        }
                                                    });
                                                }
                                                else
                                                {
                                                    Token token;

                                                    if (Tokens.GetToken(transfer.symbol, PlatformKind.Neo, out token))
                                                    {
                                                        var amount = System.Numerics.BigInteger.Parse(UnitConversion.ToBigInteger(transfer.amount, token.decimals).ToString());

                                                        var nep5 = new NEP5(neoApi, Tokens.GetTokenHash(token, PlatformKind.Neo));
                                                        CoroutineUtils.StartThrowingCoroutine(this, nep5.Transfer(unspent, keys, transfer.destination, amount, transfer.interop, Settings.neoGasFee,
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
                                                        }, (error, msg) =>
                                                        {
                                                            if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                                                            {
                                                                ChangeFaultyRPCURL(PlatformKind.Neo);
                                                            }
                                                            callback(Hash.Null, msg);
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

                                            }, (error, msg) =>
                                            {
                                                if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                                                {
                                                    ChangeFaultyRPCURL(PlatformKind.Neo);
                                                }
                                                callback(Hash.Null, msg);
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

                case PlatformKind.Ethereum:
                    {
                        try
                        {
                            var transfer = Serialization.Unserialize<TransferRequest>(script);

                            if (transfer.amount <= 0)
                            {
                                callback(Hash.Null, $"invalid transfer amount: {transfer.amount}");
                            }
                            else
                            if (transfer.platform == CurrentPlatform)
                            {
                                switch (transfer.platform)
                                {
                                    case PlatformKind.Ethereum:
                                        {
                                            var keys = EthereumKey.FromWIF(transfer.key);

                                            if (transfer.symbol == "ETH")
                                            {
                                                StartCoroutine(ethereumApi.GetNonce(keys.Address,
                                                (nonce) =>
                                                {
                                                    var hexTx = ethereumApi.SignTransaction(keys, nonce, transfer.destination,
                                                        UnitConversion.ToBigInteger(transfer.amount, 18), // Convert to WEI
                                                        Settings.ethereumGasPriceGwei * 1000000000, // Converting to WEI
                                                        Settings.ethereumTransferGasLimit);

                                                    StartCoroutine(ethereumApi.SendRawTransaction(hexTx, callback, (error, msg) =>
                                                    {
                                                        callback(Hash.Null, msg);
                                                    }));
                                                },
                                                (error, msg) =>
                                                {
                                                    callback(Hash.Null, msg);
                                                }));
                                            }
                                            else
                                            {
                                                if (Tokens.GetToken(transfer.symbol, PlatformKind.Ethereum, out Token ethToken))
                                                {
                                                    StartCoroutine(ethereumApi.GetNonce(keys.Address,
                                                    (nonce) =>
                                                    {
                                                        var gasLimit = Settings.ethereumTokenTransferGasLimit;
                                                        if (SearchInteropMapForAddress(PlatformKind.Ethereum) == transfer.destination)
                                                        {
                                                            gasLimit = Settings.ethereumTokenTransferGasLimit;
                                                        }

                                                        var hexTx = ethereumApi.SignTokenTransaction(keys, nonce,
                                                            Tokens.GetTokenHash(ethToken, PlatformKind.Ethereum),
                                                            transfer.destination,
                                                            UnitConversion.ToBigInteger(transfer.amount, ethToken.decimals),
                                                            Settings.ethereumGasPriceGwei * 1000000000, // Converting to WEI
                                                            gasLimit);

                                                        StartCoroutine(ethereumApi.SendRawTransaction(hexTx, callback, (error, msg) =>
                                                        {
                                                            callback(Hash.Null, msg);
                                                        }));
                                                    },
                                                    (error, msg) =>
                                                    {
                                                        callback(Hash.Null, msg);
                                                    }));
                                                }
                                                else
                                                {
                                                    callback(Hash.Null, $"Token {transfer.symbol} not supported");
                                                }
                                            }

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

                case PlatformKind.BSC:
                    {
                        try
                        {
                            var transfer = Serialization.Unserialize<TransferRequest>(script);

                            if (transfer.amount <= 0)
                            {
                                callback(Hash.Null, $"invalid transfer amount: {transfer.amount}");
                            }
                            else
                            if (transfer.platform == CurrentPlatform)
                            {
                                switch (transfer.platform)
                                {
                                    case PlatformKind.BSC:
                                        {
                                            var keys = EthereumKey.FromWIF(transfer.key);

                                            if (transfer.symbol == "BNB")
                                            {
                                                StartCoroutine(binanceSmartChainApi.GetNonce(keys.Address,
                                                (nonce) =>
                                                {
                                                    var hexTx = binanceSmartChainApi.SignTransaction(keys, nonce, transfer.destination,
                                                        UnitConversion.ToBigInteger(transfer.amount, 18), // Convert to WEI
                                                        Settings.binanceSmartChainGasPriceGwei * 1000000000, // Converting to WEI
                                                        Settings.binanceSmartChainTransferGasLimit);

                                                    StartCoroutine(binanceSmartChainApi.SendRawTransaction(hexTx, callback, (error, msg) =>
                                                    {
                                                        if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                                                        {
                                                            ChangeFaultyRPCURL(PlatformKind.BSC);
                                                        }
                                                        callback(Hash.Null, msg);
                                                    }));
                                                },
                                                (error, msg) =>
                                                {
                                                    if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                                                    {
                                                        ChangeFaultyRPCURL(PlatformKind.BSC);
                                                    }
                                                    callback(Hash.Null, msg);
                                                }));
                                            }
                                            else
                                            {
                                                if (Tokens.GetToken(transfer.symbol, PlatformKind.BSC, out Token bscToken))
                                                {
                                                    StartCoroutine(binanceSmartChainApi.GetNonce(keys.Address,
                                                    (nonce) =>
                                                    {
                                                        var gasLimit = Settings.binanceSmartChainTokenTransferGasLimit;
                                                        if (SearchInteropMapForAddress(PlatformKind.BSC) == transfer.destination)
                                                        {
                                                            gasLimit = Settings.binanceSmartChainTokenTransferGasLimit;
                                                        }

                                                        if (transfer.symbol == "SPE")
                                                            gasLimit = 600000; // Hack for SPE token

                                                        var hexTx = binanceSmartChainApi.SignTokenTransaction(keys, nonce,
                                                            Tokens.GetTokenHash(bscToken, PlatformKind.BSC),
                                                            transfer.destination,
                                                            UnitConversion.ToBigInteger(transfer.amount, bscToken.decimals),
                                                            Settings.binanceSmartChainGasPriceGwei * 1000000000, // Converting to WEI
                                                            gasLimit);

                                                        StartCoroutine(binanceSmartChainApi.SendRawTransaction(hexTx, callback, (error, msg) =>
                                                        {
                                                            if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                                                            {
                                                                ChangeFaultyRPCURL(PlatformKind.BSC);
                                                            }
                                                            callback(Hash.Null, msg);
                                                        }));
                                                    },
                                                    (error, msg) =>
                                                    {
                                                        if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                                                        {
                                                            ChangeFaultyRPCURL(PlatformKind.BSC);
                                                        }
                                                        callback(Hash.Null, msg);
                                                    }));
                                                }
                                                else
                                                {
                                                    callback(Hash.Null, $"Token {transfer.symbol} not supported");
                                                }
                                            }

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

                default:
                    {
                        callback(Hash.Null, "not implemented for " + CurrentPlatform);
                        break;
                    }
            }
        }

        public void InvokeScript(string chain, byte[] script, Action<string[], string> callback)
        {
            var account = this.CurrentAccount;

            switch (CurrentPlatform)
            {
                case PlatformKind.Phantasma:
                    {
                        Log.Write("InvokeScript: " + System.Text.Encoding.UTF8.GetString(script), Log.Level.Debug1);
                        StartCoroutine(phantasmaApi.InvokeRawScript(chain, Base16.Encode(script), (x) =>
                        {
                            Log.Write("InvokeScript result: " + x.result, Log.Level.Debug1);
                            callback(x.results, null);
                        }, (error, log) =>
                        {
                            if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                            {
                                ChangeFaultyRPCURL(PlatformKind.Phantasma);
                            }
                            callback(null, log);
                        }));
                        break;
                    }
                default:
                    {
                        callback(null, "not implemented for " + CurrentPlatform);
                        break;
                    }
            }
        }

        public void InvokeScriptPhantasma(string chain, byte[] script, Action<byte[], string> callback)
        {
            var account = this.CurrentAccount;

            Log.Write("InvokeScriptPhantasma: " + System.Text.Encoding.UTF8.GetString(script), Log.Level.Debug1);
            StartCoroutine(phantasmaApi.InvokeRawScript(chain, Base16.Encode(script), (x) =>
            {
                Log.Write("InvokeScriptPhantasma result: " + x.result, Log.Level.Debug1);
                callback(Base16.Decode(x.result), null);
            }, (error, log) =>
            {
                if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                {
                    ChangeFaultyRPCURL(PlatformKind.Phantasma);
                }
                callback(null, log);
            }));
        }

        public void GetArchive(Hash hash, Action<bool, Archive, string> callback)
        {
            var account = this.CurrentAccount;

            switch (CurrentPlatform)
            {
                case PlatformKind.Phantasma:
                    {
                        Log.Write("GetArchive: " + hash, Log.Level.Debug1);
                        StartCoroutine(phantasmaApi.GetArchive(hash.ToString(), (result) =>
                        {
                            Log.Write("GetArchive result: " + result, Log.Level.Debug1);
                            callback(true, result, null);
                        }, (error, log) =>
                        {
                            if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                            {
                                ChangeFaultyRPCURL(PlatformKind.Phantasma);
                            }
                            callback(false, new Archive(), log);
                        }));
                        break;
                    }
                default:
                    {
                        callback(false, new Archive(), "not implemented for " + CurrentPlatform);
                        break;
                    }
            }
        }

        public void ReadArchive(Hash hash, int blockIndex, Action<bool, byte[], string> callback)
        {
            var account = this.CurrentAccount;

            switch (CurrentPlatform)
            {
                case PlatformKind.Phantasma:
                    {
                        Log.Write("ReadArchive: " + hash, Log.Level.Debug1);
                        StartCoroutine(phantasmaApi.ReadArchive(hash.ToString(), blockIndex, (result) =>
                        {
                            Log.Write("ReadArchive result: " + result, Log.Level.Debug1);
                            callback(true, result, null);
                        }, (error, log) =>
                        {
                            if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                            {
                                ChangeFaultyRPCURL(PlatformKind.Phantasma);
                            }
                            callback(false, null, log);
                        }));
                        break;
                    }
                default:
                    {
                        callback(false, null, "not implemented for " + CurrentPlatform);
                        break;
                    }
            }
        }

        public void WriteArchive(Hash hash, int blockIndex, byte[] data, Action<bool, string> callback)
        {
            var account = this.CurrentAccount;

            switch (CurrentPlatform)
            {
                case PlatformKind.Phantasma:
                    {
                        Log.Write("WriteArchive: " + hash, Log.Level.Debug1);
                        StartCoroutine(phantasmaApi.WriteArchive(hash.ToString(), blockIndex, data, (result) =>
                        {
                            Log.Write("WriteArchive result: " + result, Log.Level.Debug1);
                            callback(result, null);
                        }, (error, log) =>
                        {
                            if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                            {
                                ChangeFaultyRPCURL(PlatformKind.Phantasma);
                            }
                            callback(false, log);
                        }));
                        break;
                    }
                default:
                    {
                        callback(false, "not implemented for " + CurrentPlatform);
                        break;
                    }
            }
        }

        private DateTime _lastNftRefresh = DateTime.MinValue;
        private string _lastNftRefreshSymbol = "";

        // We use this to detect when account was just loaded
        // and needs balances/histories to be loaded.
        public bool accountBalanceNotLoaded = true;
        public bool accountHistoryNotLoaded = true;

        public void SelectAccount(int index)
        {
            _lastNftRefresh = DateTime.MinValue;
            _lastNftRefreshSymbol = "";
            _selectedAccountIndex = index;
            CurrentPasswordHash = "";

            _accountInitialized = false;

            var platforms = CurrentAccount.platforms.Split();

            // We should add Ethereum platform to old accounts.
            if (!platforms.Contains(PlatformKind.Ethereum))
            {
                var account = Accounts[_selectedAccountIndex];
                account.platforms |= PlatformKind.Ethereum;
                Accounts[_selectedAccountIndex] = account;

                _states[PlatformKind.Ethereum] = new AccountState()
                {
                    platform = PlatformKind.Ethereum,
                    address = GetAddress(CurrentIndex, PlatformKind.Ethereum),
                    balances = new Balance[0],
                    flags = AccountFlags.None,
                    name = ValidationUtils.ANONYMOUS_NAME,
                };

                SaveAccounts();

                platforms.Add(PlatformKind.Ethereum);
            }

            // We should add BinanceSmartChain platform to old accounts.
            if (!platforms.Contains(PlatformKind.BSC))
            {
                var account = Accounts[_selectedAccountIndex];
                account.platforms |= PlatformKind.BSC;
                Accounts[_selectedAccountIndex] = account;

                _states[PlatformKind.BSC] = new AccountState()
                {
                    platform = PlatformKind.BSC,
                    address = GetAddress(CurrentIndex, PlatformKind.BSC),
                    balances = new Balance[0],
                    flags = AccountFlags.None,
                    name = ValidationUtils.ANONYMOUS_NAME,
                };

                SaveAccounts();

                platforms.Add(PlatformKind.BSC);
            }

            CurrentPlatform = platforms.FirstOrDefault();
            _states.Clear();

            accountBalanceNotLoaded = true;
            accountHistoryNotLoaded = true;
        }

        public void UnselectAcount()
        {
            _selectedAccountIndex = -1;

            _accountInitialized = false;

            // revoke all dapps connected to this account via Phantasma Link
            if (_states.ContainsKey(PlatformKind.Phantasma))
            {
                var link = ConnectorManager.Instance.PhantasmaLink;

                var state = _states[PlatformKind.Phantasma];
                foreach (var entry in state.dappTokens)
                {
                    link.Revoke(entry.Key, entry.Value);
                }
            }

            _states.Clear();
            _nfts.Clear();
            TtrsStore.Clear();
            GameStore.Clear();
            NftImages.Clear();
            _refreshStatus.Clear();
        }

        private void ReportWalletBalance(PlatformKind platform, AccountState state)
        {
            try
            {
                RefreshStatus refreshStatus;
                lock (_refreshStatus)
                {
                    refreshStatus = _refreshStatus[platform];
                    refreshStatus.BalanceRefreshing = false;
                    _refreshStatus[platform] = refreshStatus;
                }

                if (state != null)
                {
                    Log.Write("Received new state for " + platform);
                    _states[platform] = state;

                    //if (!_accountInitialized && GetWorthOfPlatform(platform) > GetWorthOfPlatform(CurrentPlatform))
                    //{
                    //    CurrentPlatform = platform;
                    //}
                }

                _accountInitialized = true;
            
                var temp = refreshStatus.BalanceRefreshCallback;
                lock (_refreshStatus)
                {
                    refreshStatus.BalanceRefreshCallback = null;
                    _refreshStatus[platform] = refreshStatus;
                }
                temp?.Invoke();
            }
            catch (Exception) { } // This fixes crash when user leaves account fast without waiting for balances to load
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

        private void ReportWalletNft(PlatformKind platform, string symbol)
        {
            if (_nfts.ContainsKey(platform) && _nfts[platform] != null)
            {
                Log.Write($"Received {_nfts[platform].Count()} new {symbol} NFTs for {platform}");

                if (CurrentPlatform == PlatformKind.None)
                {
                    CurrentPlatform = platform;
                }
            }
        }

        private void ReportWalletHistory(PlatformKind platform, List<HistoryEntry> history)
        {
            try
            {
                lock (_refreshStatus)
                {
                    var refreshStatus = _refreshStatus[platform];
                    refreshStatus.HistoryRefreshing = false;
                    _refreshStatus[platform] = refreshStatus;
                }

                if (history != null)
                {
                    Log.Write("Received new history for " + platform);
                    _history[platform] = history.ToArray();

                    if (CurrentPlatform == PlatformKind.None)
                    {
                        CurrentPlatform = platform;
                    }
                }
            }
            catch (Exception) { } // This fixes crash when user leaves account fast without waiting for balances to load
        }


        private const int maxChecks = 12; // Timeout after 36 seconds

        public void RequestConfirmation(string transactionHash, int checkCount, Action<string> callback)
        {
            switch (CurrentPlatform)
            {
                case PlatformKind.Phantasma:
                    StartCoroutine(phantasmaApi.GetTransaction(transactionHash, (tx) =>
                    {
                        callback(null);
                    }, (error, msg) =>
                    {
                        if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                        {
                            ChangeFaultyRPCURL(PlatformKind.Phantasma);
                        }

                        if (checkCount <= maxChecks)
                        {
                            callback(msg);
                        }
                        else
                        {
                            callback("timeout");
                        }
                    }));
                    break;

                case PlatformKind.Neo:
                    var url = GetNeoscanAPIUrl($"get_transaction/{transactionHash}");

                    StartCoroutine(WebClient.RESTRequest(url, WebClient.NoTimeout, (error, msg) =>
                    {
                        if (checkCount <= maxChecks)
                        {
                            callback("pending");
                        }
                        else
                        {
                            callback("timeout");
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
                            if (checkCount <= maxChecks)
                            {
                                callback("pending");
                            }
                            else
                            {
                                callback("timeout");
                            }
                        }
                    }));
                    break;

                case PlatformKind.Ethereum:
                case PlatformKind.BSC:
                    // For Ethereum we should return immediately
                    // since it's unpredictable if we would be able to find tx in mempool
                    // or it will appear there after several minutes.
                    // And we are not waiting for a confirmation anyway.
                    callback(null);
                    break;

                default:
                    callback("not implemented: " + CurrentPlatform);
                    break;
            }

        }

        public void RefreshBalances(bool force, PlatformKind platforms = PlatformKind.None, Action callback = null, bool allowOneUserRefreshAfterExecution = false)
        {
            List<PlatformKind> platformsList;
            if(platforms == PlatformKind.None)
                platformsList = CurrentAccount.platforms.Split();
            else
                platformsList = platforms.Split();

            lock (_refreshStatus)
            {
                foreach (var platform in platformsList)
                {
                    RefreshStatus refreshStatus;
                    var now = DateTime.UtcNow;
                    if (_refreshStatus.ContainsKey(platform))
                    {
                        refreshStatus = _refreshStatus[platform];

                        var diff = now - refreshStatus.LastBalanceRefresh;

                        if (!force && diff.TotalSeconds < 30)
                        {
                            var temp = refreshStatus.BalanceRefreshCallback;
                            refreshStatus.BalanceRefreshCallback = null;
                            _refreshStatus[platform] = refreshStatus;
                            temp?.Invoke();
                            return;
                        }

                        refreshStatus.BalanceRefreshing = true;
                        refreshStatus.LastBalanceRefresh = allowOneUserRefreshAfterExecution ? DateTime.MinValue : now;
                        refreshStatus.BalanceRefreshCallback = callback;

                        _refreshStatus[platform] = refreshStatus;
                    }
                    else
                    {
                        _refreshStatus.Add(platform,
                            new RefreshStatus
                            {
                                BalanceRefreshing = true,
                                LastBalanceRefresh = allowOneUserRefreshAfterExecution ? DateTime.MinValue : now,
                                BalanceRefreshCallback = callback,
                                HistoryRefreshing = false,
                                LastHistoryRefresh = DateTime.MinValue
                            });
                    }
                }
            }

            var wif = CurrentWif;

            foreach (var platform in platformsList)
            {
                lock (Tokens.__lockObj)
                {
                    switch (platform)
                    {
                        case PlatformKind.Phantasma:
                        {
                            var keys = PhantasmaKeys.FromWIF(wif);
                            var ethKeys = EthereumKey.FromWIF(wif);
                            StartCoroutine(phantasmaApi.GetAccount(keys.Address.Text, (acc) =>
                                {
                                    var balanceMap = new Dictionary<string, Balance>();

                                    foreach (var entry in acc.balances)
                                    {

                                        var token = Tokens.GetToken(entry.symbol, PlatformKind.Phantasma);
                                        if (token != null)
                                            balanceMap[entry.symbol] = new Balance()
                                            {
                                                Symbol = entry.symbol,
                                                Available = AmountFromString(entry.amount, token.decimals),
                                                Pending = 0,
                                                Staked = 0,
                                                Claimable = 0,
                                                Chain = entry.chain,
                                                Decimals = token.decimals,
                                                Burnable = token.IsBurnable(),
                                                Fungible = token.IsFungible(),
                                                Ids = entry.ids
                                            };
                                        else
                                            balanceMap[entry.symbol] = new Balance()
                                            {
                                                Symbol = entry.symbol,
                                                Available = AmountFromString(entry.amount, 8),
                                                Pending = 0,
                                                Staked = 0,
                                                Claimable = 0,
                                                Chain = entry.chain,
                                                Decimals = 8,
                                                Burnable = true,
                                                Fungible = true,
                                                Ids = entry.ids
                                            };


                                    }

                                    var stakedAmount = AmountFromString(acc.stake.amount,
                                        Tokens.GetTokenDecimals("SOUL", PlatformKind.Phantasma));
                                    var claimableAmount = AmountFromString(acc.stake.unclaimed,
                                        Tokens.GetTokenDecimals("KCAL", PlatformKind.Phantasma));

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
                                            var token = Tokens.GetToken(symbol, PlatformKind.Phantasma);
                                            var entry = new Balance()
                                            {
                                                Symbol = symbol,
                                                Chain = "main",
                                                Available = 0,
                                                Staked = stakedAmount,
                                                Claimable = 0,
                                                Pending = 0,
                                                Decimals = token.decimals,
                                                Burnable = token.IsBurnable(),
                                                Fungible = token.IsFungible()
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
                                            var token = Tokens.GetToken(symbol, PlatformKind.Phantasma);
                                            var entry = new Balance()
                                            {
                                                Symbol = symbol,
                                                Chain = "main",
                                                Available = 0,
                                                Staked = 0,
                                                Claimable = claimableAmount,
                                                Pending = 0,
                                                Decimals = token.decimals,
                                                Burnable = token.IsBurnable(),
                                                Fungible = token.IsFungible()
                                            };
                                            balanceMap[symbol] = entry;
                                        }
                                    }

                                    // State without swaps
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

                                    state.usedStorage = acc.storage.used;
                                    state.availableStorage = acc.storage.available;
                                    state.archives = acc.storage.archives;
                                    state.avatarData = acc.storage.avatar;

                                    ReportWalletBalance(platform, state);

                                    // Swaps to Pha from Neo are reported here.
                                    RequestPendings(keys.Address.Text, PlatformKind.Phantasma, (phaSwaps, phaError) =>
                                    {
                                        if (phaSwaps != null)
                                        {
                                            MergeSwaps(PlatformKind.Phantasma, balanceMap, phaSwaps);
                                        }
                                        else
                                        {
                                            Log.WriteWarning(phaError);
                                        }

                                        // Swaps to Pha from ETH are reported here.
                                        RequestPendings(ethKeys.Address, PlatformKind.Ethereum, (swapsFromEth, error) =>
                                        {
                                            if (swapsFromEth != null)
                                            {
                                                MergeSwaps(PlatformKind.Phantasma, balanceMap, swapsFromEth);
                                            }
                                            else
                                            {
                                                Log.WriteWarning(error);
                                            }

                                            // Swaps to Pha from BSC are reported here.
                                            RequestPendings(ethKeys.Address, PlatformKind.BSC, (swapsFromBsc, error2) =>
                                            {
                                                if (swapsFromBsc != null)
                                                {
                                                    MergeSwaps(PlatformKind.Phantasma, balanceMap, swapsFromBsc);
                                                }
                                                else
                                                {
                                                    Log.WriteWarning(error2);
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

                                                if (acc.validator.Equals("Primary") ||
                                                    acc.validator.Equals("Secondary"))
                                                {
                                                    state.flags |= AccountFlags.Validator;
                                                }

                                                state.stakeTime = stakeTimestamp;

                                                state.usedStorage = acc.storage.used;
                                                state.availableStorage = acc.storage.available;
                                                state.archives = acc.storage.archives;
                                                state.avatarData = acc.storage.avatar;

                                                ReportWalletBalance(platform, state);
                                            });
                                        });
                                    });
                                },
                                (error, msg) =>
                                {
                                    Log.WriteWarning($"RefreshBalances[PHA] {error}: {msg}");

                                    if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                                    {
                                        ChangeFaultyRPCURL(PlatformKind.Phantasma);
                                    }

                                    ReportWalletBalance(platform, null);
                                }));
                        }
                            break;

                        case PlatformKind.Neo:
                        {
                            var keys = NeoKeys.FromWIF(wif);

                            var url = GetNeoscanAPIUrl($"get_balance/{keys.Address}");

                            StartCoroutine(WebClient.RESTRequest(url, WebClient.DefaultTimeout,
                                (error, msg) => { ReportWalletBalance(platform, null); },
                                (response) =>
                                {
                                    var balances = new List<Balance>();

                                    var neoTokens = Tokens.GetTokens(PlatformKind.Neo);

                                    var balance = response.GetNode("balance");
                                    foreach (var entry in balance.Children)
                                    {
                                        var hash = entry.GetString("asset_hash");
                                        var symbol = entry.GetString("asset_symbol");
                                        var amount = entry.GetDecimal("amount");

                                        Token token;

                                        if (Tokens.GetToken(symbol, PlatformKind.Neo, out token))
                                        {
                                            if (hash.ToUpper() ==
                                                Tokens.GetTokenHash(token, PlatformKind.Neo).ToUpper())
                                            {
                                                balances.Add(new Balance()
                                                {
                                                    Symbol = symbol,
                                                    Available = amount,
                                                    Pending = 0,
                                                    Claimable = 0, // TODO support claimable GAS
                                                    Staked = 0,
                                                    Chain = "main",
                                                    Decimals = token.decimals,
                                                    Burnable = token.IsBurnable(),
                                                    Fungible = token.IsFungible()
                                                });
                                            }
                                        }
                                    }

                                    CoroutineUtils.StartThrowingCoroutine(this, neoApi.GetUnclaimed(keys.Address,
                                        (amount) =>
                                        {
                                            var balanceMap = new Dictionary<string, Balance>();

                                            foreach (var neoToken in neoTokens)
                                            {
                                                var tokenBalance = balances.Where(x =>
                                                    x.Symbol.ToUpper() == neoToken.symbol.ToUpper()).SingleOrDefault();

                                                if (tokenBalance != null)
                                                {
                                                    balanceMap[tokenBalance.Symbol] = tokenBalance;

                                                    if (tokenBalance.Symbol.ToUpper() == "GAS")
                                                    {
                                                        tokenBalance.Claimable += amount;
                                                    }
                                                }
                                                else
                                                {
                                                    if (neoToken.symbol.ToUpper() == "GAS" && amount > 0)
                                                    {
                                                        // We should show GAS even if its balance is 0
                                                        // if there's some GAS to be claimed.
                                                        balanceMap[neoToken.symbol] = new Balance()
                                                        {
                                                            Symbol = neoToken.symbol,
                                                            Available = 0,
                                                            Pending = 0,
                                                            Claimable = amount,
                                                            Staked = 0,
                                                            Chain = "main",
                                                            Decimals = neoToken.decimals,
                                                            Burnable = neoToken.IsBurnable(),
                                                            Fungible = neoToken.IsFungible()
                                                        };
                                                    }
                                                }
                                            }

                                            // State before swaps
                                            var state = new AccountState()
                                            {
                                                platform = platform,
                                                address = keys.Address,
                                                name = ValidationUtils.ANONYMOUS_NAME, // TODO support NNS
                                                balances = balanceMap.Values.ToArray(),
                                                flags = AccountFlags.None
                                            };
                                            ReportWalletBalance(platform, state);

                                            RequestPendings(keys.Address, PlatformKind.Neo, (swaps, error) =>
                                            {
                                                if (swaps != null)
                                                {
                                                    MergeSwaps(PlatformKind.Neo, balanceMap, swaps);
                                                }
                                                else
                                                {
                                                    Log.WriteWarning(error);
                                                }

                                                var state = new AccountState()
                                                {
                                                    platform = platform,
                                                    address = keys.Address,
                                                    name = ValidationUtils.ANONYMOUS_NAME, // TODO support NNS
                                                    balances = balanceMap.Values.ToArray(),
                                                    flags = AccountFlags.None
                                                };
                                                ReportWalletBalance(platform, state);
                                            });
                                        },
                                        (error, msg) =>
                                        {
                                            if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                                            {
                                                ChangeFaultyRPCURL(PlatformKind.Neo);
                                            }

                                            ReportWalletBalance(platform, null);
                                        }), ex =>
                                    {
                                        if (ex != null)
                                        {
                                            Log.WriteWarning($"RefreshBalances[NEO] {ex}");
                                            ReportWalletBalance(platform, null);
                                        }
                                    });

                                }));
                        }
                            break;

                        case PlatformKind.Ethereum:
                        {
                            var keys = EthereumKey.FromWIF(wif);

                            var ethTokens = Tokens.GetTokens(PlatformKind.Ethereum);
                            var balances = new List<Balance>();

                            Action onLoadFinish = new Action(() =>
                            {
                                var balanceMap = new Dictionary<string, Balance>();
                                foreach (var ethToken in ethTokens)
                                {
                                    var tokenBalance = balances
                                        .Where(x => x.Symbol.ToUpper() == ethToken.symbol.ToUpper()).SingleOrDefault();
                                    if (tokenBalance != null)
                                        balanceMap[tokenBalance.Symbol] = tokenBalance;
                                }

                                var ethereumAddressUtil = new Poltergeist.PhantasmaLegacy.Ethereum.Util.AddressUtil();

                                // State without swaps
                                var state = new AccountState()
                                {
                                    platform = platform,
                                    address = ethereumAddressUtil.ConvertToChecksumAddress(keys.Address),
                                    name = ValidationUtils.ANONYMOUS_NAME, // TODO support NNS
                                    balances = balanceMap.Values.ToArray(),
                                    flags = AccountFlags.None
                                };
                                ReportWalletBalance(platform, state);

                                RequestPendings(keys.Address, PlatformKind.Ethereum, (swaps, error) =>
                                {
                                    if (swaps != null)
                                    {
                                        MergeSwaps(PlatformKind.Ethereum, balanceMap, swaps);
                                    }
                                    else
                                    {
                                        Log.WriteWarning(error);
                                    }

                                    var state = new AccountState()
                                    {
                                        platform = platform,
                                        address = ethereumAddressUtil.ConvertToChecksumAddress(keys.Address),
                                        name = ValidationUtils.ANONYMOUS_NAME, // TODO support NNS
                                        balances = balanceMap.Values.ToArray(),
                                        flags = AccountFlags.None
                                    };
                                    ReportWalletBalance(platform, state);
                                });
                            });

                            foreach (var ethToken in ethTokens)
                            {
                                if (ethToken.symbol == "ETH")
                                {
                                    StartCoroutine(ethereumApi.GetBalance(keys.Address, ethToken.symbol,
                                        ethToken.decimals, (balance) =>
                                        {
                                            balances.Add(balance);

                                            if (balances.Count() == ethTokens.Count())
                                            {
                                                onLoadFinish();
                                            }
                                        },
                                        (error, msg) =>
                                        {
                                            Log.WriteWarning($"RefreshBalances[ETH/1] {error}: {msg}");
                                            ReportWalletBalance(platform, null);
                                        }));
                                }
                                else
                                {
                                    StartCoroutine(ethereumApi.GetTokenBalance(keys.Address,
                                        Tokens.GetTokenHash(ethToken, PlatformKind.Ethereum),
                                        ethToken.symbol, ethToken.decimals, (balanceSoul) =>
                                        {
                                            balances.Add(balanceSoul);

                                            if (balances.Count() == ethTokens.Count())
                                            {
                                                onLoadFinish();
                                            }
                                        },
                                        (error, msg) =>
                                        {
                                            Log.WriteWarning($"RefreshBalances[ETH/2] {error}: {msg}");
                                            ReportWalletBalance(platform, null);
                                        }));
                                }
                            }
                        }
                            break;

                        case PlatformKind.BSC:
                        {
                            var keys = EthereumKey.FromWIF(wif);

                            var bscTokens = Tokens.GetTokens(PlatformKind.BSC);
                            var balances = new List<Balance>();

                            Action onLoadFinish = new Action(() =>
                            {
                                var balanceMap = new Dictionary<string, Balance>();
                                foreach (var bscToken in bscTokens)
                                {
                                    var tokenBalance = balances
                                        .Where(x => x.Symbol.ToUpper() == bscToken.symbol.ToUpper()).SingleOrDefault();
                                    if (tokenBalance != null)
                                        balanceMap[tokenBalance.Symbol] = tokenBalance;
                                }

                                var ethereumAddressUtil = new Poltergeist.PhantasmaLegacy.Ethereum.Util.AddressUtil();

                                // State without swaps
                                var state = new AccountState()
                                {
                                    platform = platform,
                                    address = ethereumAddressUtil.ConvertToChecksumAddress(keys.Address),
                                    name = ValidationUtils.ANONYMOUS_NAME, // TODO support NNS
                                    balances = balanceMap.Values.ToArray(),
                                    flags = AccountFlags.None
                                };
                                ReportWalletBalance(platform, state);

                                RequestPendings(keys.Address, PlatformKind.BSC, (swaps, error) =>
                                {
                                    if (swaps != null)
                                    {
                                        MergeSwaps(PlatformKind.BSC, balanceMap, swaps);
                                    }
                                    else
                                    {
                                        Log.WriteWarning(error);
                                    }

                                    var state = new AccountState()
                                    {
                                        platform = platform,
                                        address = ethereumAddressUtil.ConvertToChecksumAddress(keys.Address),
                                        name = ValidationUtils.ANONYMOUS_NAME, // TODO support NNS
                                        balances = balanceMap.Values.ToArray(),
                                        flags = AccountFlags.None
                                    };
                                    ReportWalletBalance(platform, state);
                                });
                            });

                            foreach (var bscToken in bscTokens)
                            {
                                if (bscToken.symbol == "BNB")
                                {
                                    StartCoroutine(binanceSmartChainApi.GetBalance(keys.Address, bscToken.symbol,
                                        bscToken.decimals, (balance) =>
                                        {
                                            balances.Add(balance);

                                            if (balances.Count() == bscTokens.Count())
                                            {
                                                onLoadFinish();
                                            }
                                        },
                                        (error, msg) =>
                                        {
                                            Log.WriteWarning($"RefreshBalances[BSC/1] {error}: {msg}");
                                            if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                                            {
                                                ChangeFaultyRPCURL(PlatformKind.BSC);
                                            }

                                            ReportWalletBalance(platform, null);
                                        }));
                                }
                                else
                                {
                                    StartCoroutine(binanceSmartChainApi.GetTokenBalance(keys.Address,
                                        Tokens.GetTokenHash(bscToken, PlatformKind.BSC),
                                        bscToken.symbol, bscToken.decimals, (balanceSoul) =>
                                        {
                                            balances.Add(balanceSoul);

                                            if (balances.Count() == bscTokens.Count())
                                            {
                                                onLoadFinish();
                                            }
                                        },
                                        (error, msg) =>
                                        {
                                            Log.WriteWarning($"RefreshBalances[BSC/2] {error}: {msg}");
                                            if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                                            {
                                                ChangeFaultyRPCURL(PlatformKind.BSC);
                                            }

                                            ReportWalletBalance(platform, null);
                                        }));
                                }
                            }
                        }
                            break;

                        default:
                            ReportWalletBalance(platform, null);
                            break;
                    }
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
                    name = ValidationUtils.ANONYMOUS_NAME,
                };
            }
        }

        private void MergeSwaps(PlatformKind platform, Dictionary<string, Balance> balanceMap, Swap[] swaps)
        {
            var platformName = platform.ToString().ToLower();

            foreach (var swap in swaps)
            {
                if (swap.destinationPlatform != platformName)
                {
                    continue;
                }

                if (swap.destinationHash != "pending")
                {
                    continue;
                }

                var token = Tokens.GetToken(swap.symbol, platform);
                var amount = AmountFromString(swap.value, token.decimals);

                Log.Write($"Found pending {platformName} swap: {amount} {swap.symbol}");

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
                        Decimals = token.decimals,
                        Burnable = token.IsBurnable(),
                        Fungible = token.IsFungible(),
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

            this.Accounts = accounts;
            SaveAccounts();
        }

        internal void DeleteAll()
        {
            this.Accounts = new List<Account>();
        }

        public void RefreshNft(bool force, string symbol)
        {
            var now = DateTime.UtcNow;
            var diff = now - _lastNftRefresh;

            if (!force && diff.TotalSeconds < 30 && _lastNftRefreshSymbol == symbol)
            {
                return;
            }

            if (force)
            {
                // On force refresh we clear NFT symbol's cache.
                if (symbol.ToUpper() == "TTRS")
                    TtrsStore.Clear();
                else if (symbol.ToUpper() == "GAME")
                    GameStore.Clear();
                else
                    Cache.ClearDataNode("tokens-" + symbol.ToLower(), Cache.FileType.JSON, CurrentState.address);

                NftImages.Clear(symbol);
            }

            _lastNftRefresh = now;
            _lastNftRefreshSymbol = symbol;

            var platforms = CurrentAccount.platforms.Split();

            var wif = this.CurrentWif;

            foreach (var platform in platforms)
            {
                // Reinitializing NFT dictionary if needed.
                if (_nfts.ContainsKey(platform))
                    _nfts[platform].Clear();

                if (Tokens.GetToken(symbol, platform, out var tokenInfo))
                {
                    switch (platform)
                    {
                        case PlatformKind.Phantasma:
                            {
                                var keys = PhantasmaKeys.FromWIF(wif);

                                Log.Write("Getting NFTs...");
                                foreach (var balanceEntry in CurrentState.balances)
                                {
                                    if (balanceEntry.Symbol == symbol && !tokenInfo.IsFungible())
                                    {
                                        nftDescriptionsAreFullyLoaded = false;

                                        // Initializing NFT dictionary if needed.
                                        if (!_nfts.ContainsKey(platform))
                                            _nfts.Add(platform, new List<TokenData>());

                                        var cache = Cache.GetDataNode("tokens-" + symbol.ToLower(), Cache.FileType.JSON, 0, CurrentState.address);

                                        if (cache == null)
                                        {
                                            cache = DataNode.CreateObject();
                                        }
                                        DataNode cachedTokens;
                                        if (cache.HasNode("tokens-" + symbol.ToLower()))
                                            cachedTokens = cache.GetNode("tokens-" + symbol.ToLower());
                                        else
                                            cachedTokens = cache.AddNode(DataNode.CreateObject("tokens-" + symbol.ToLower()));

                                        int loadedTokenCounter = 0;
                                        foreach (var id in balanceEntry.Ids)
                                        {
                                            // Checking if token is cached.
                                            DataNode token = null;
                                            foreach (var cachedToken in cachedTokens.Children)
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
                                                if (!_nfts[platform].Exists(x => x.ID == tokenId))
                                                {
                                                    var tokenData = TokenData.FromNode(token, symbol);
                                                    _nfts[platform].Add(tokenData);

                                                    // Downloading NFT images.
                                                    StartCoroutine(NftImages.DownloadImage(symbol, tokenData.GetPropertyValue("ImageURL"), id));
                                                }

                                                if (loadedTokenCounter == balanceEntry.Ids.Length)
                                                {
                                                    // We finished loading tokens.
                                                    // Saving them in cache.
                                                    Cache.AddDataNode("tokens-" + symbol.ToLower(), Cache.FileType.JSON, cache, CurrentState.address);
                                                    
                                                    if (symbol != "TTRS")
                                                    {
                                                        // For all NFTs except TTRS all needed information
                                                        // is loaded by this moment.
                                                        nftDescriptionsAreFullyLoaded = true;
                                                    }

                                                    ReportWalletNft(platform, symbol);
                                                }
                                            }
                                            else
                                            {
                                                if (symbol == "TTRS")
                                                {
                                                    // TODO: Load TokenData for TTRS too (add batch load method for TokenDatas).
                                                    // For now we skip TokenData loading to speed up TTRS NFTs loading,
                                                    // since it's not used for TTRS anyway.
                                                    var tokenData = new TokenData();
                                                    tokenData.ID = id;
                                                    _nfts[platform].Add(tokenData);

                                                    loadedTokenCounter++;
                                                }
                                                else
                                                {
                                                    StartCoroutine(phantasmaApi.GetNFT(symbol, id, (result) =>
                                                    {
                                                        var tokenData = TokenData.FromNode(result, symbol);
                                                        
                                                        // Downloading NFT images.
                                                        StartCoroutine(NftImages.DownloadImage(symbol, tokenData.GetPropertyValue("ImageURL"), id));

                                                        loadedTokenCounter++;

                                                        token = cachedTokens.AddNode(result);

                                                        _nfts[platform].Add(tokenData);

                                                        if (loadedTokenCounter == balanceEntry.Ids.Length)
                                                        {
                                                            // We finished loading tokens.
                                                            // Saving them in cache.
                                                            Cache.AddDataNode("tokens-" + symbol.ToLower(), Cache.FileType.JSON, cache, CurrentState.address);

                                                            ReportWalletNft(platform, symbol);
                                                        }
                                                    }, (error, msg) =>
                                                    {
                                                        Log.Write(msg);
                                                    }));
                                                }
                                            }
                                        }

                                        ReportWalletNft(platform, symbol);

                                        if (balanceEntry.Ids.Length > 0)
                                        {
                                            // Getting NFT descriptions.
                                            if (symbol == "TTRS")
                                            {
                                                StartCoroutine(TtrsStore.LoadStoreNft(balanceEntry.Ids, (item) =>
                                                {
                                                    // Downloading NFT images.
                                                    StartCoroutine(NftImages.DownloadImage(symbol, item.ImageUrl, item.Id));
                                                }, () =>
                                                {
                                                    nftDescriptionsAreFullyLoaded = true;
                                                }));
                                            }
                                            else if (symbol == "GAME")
                                            {
                                                StartCoroutine(GameStore.LoadStoreNft(balanceEntry.Ids, (item) =>
                                                {
                                                    // Downloading NFT images.
                                                    StartCoroutine(NftImages.DownloadImage(symbol, item.img_url, item.ID));
                                                }, () =>
                                                {
                                                    nftDescriptionsAreFullyLoaded = true;
                                                }));
                                            }
                                        }
                                    }
                                }
                            }
                            break;

                        default:
                            ReportWalletNft(platform, symbol);
                            break;
                    }
                }
                else
                {
                    ReportWalletNft(platform, symbol);
                }
            }
        }

        public void RefreshHistory(bool force, PlatformKind platforms = PlatformKind.None)
        {
            List<PlatformKind> platformsList;
            if (platforms == PlatformKind.None)
                platformsList = CurrentAccount.platforms.Split();
            else
                platformsList = platforms.Split();

            lock (_refreshStatus)
            {
                foreach (var platform in platformsList)
                {
                    RefreshStatus refreshStatus;
                    var now = DateTime.UtcNow;
                    if (_refreshStatus.ContainsKey(platform))
                    {
                        refreshStatus = _refreshStatus[platform];

                        var diff = now - refreshStatus.LastHistoryRefresh;

                        if (!force && diff.TotalSeconds < 30)
                        {
                            return;
                        }

                        refreshStatus.HistoryRefreshing = true;
                        refreshStatus.LastHistoryRefresh = now;

                        _refreshStatus[platform] = refreshStatus;
                    }
                    else
                    {
                        _refreshStatus.Add(platform,
                            new RefreshStatus
                            {
                                BalanceRefreshing = false,
                                LastBalanceRefresh = DateTime.MinValue,
                                BalanceRefreshCallback = null,
                                HistoryRefreshing = true,
                                LastHistoryRefresh = now
                            });
                    }
                }
            }

            var wif = this.CurrentWif;

            foreach (var platform in platformsList)
            {
                switch (platform)
                {
                    case PlatformKind.Phantasma:
                        {
                            var keys = PhantasmaKeys.FromWIF(wif);
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
                                if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                                {
                                    ChangeFaultyRPCURL(PlatformKind.Phantasma);
                                }
                                ReportWalletHistory(platform, null);
                            }));
                        }
                        break;

                    case PlatformKind.Neo:
                        {
                            var keys = NeoKeys.FromWIF(wif);
                            var url = GetNeoscanAPIUrl($"get_address_abstracts/{keys.Address}/1");

                            StartCoroutine(WebClient.RESTRequest(url, WebClient.DefaultTimeout, (error, msg) =>
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

                    case PlatformKind.Ethereum:
                        {
                            var keys = EthereumKey.FromWIF(wif);
                            var urlEth = GetEtherscanAPIUrl($"module=account&action=txlist&address={keys.Address}&sort=desc");

                            StartCoroutine(WebClient.RESTRequest(urlEth, WebClient.DefaultTimeout, (error, msg) =>
                            {
                                ReportWalletHistory(platform, null);
                            },
                            (responseEth) =>
                            {
                                var urlErc20 = GetEtherscanAPIUrl($"module=account&action=tokentx&address={keys.Address}&sort=desc");
                                StartCoroutine(WebClient.RESTRequest(urlErc20, WebClient.DefaultTimeout, (error, msg) =>
                                {
                                    ReportWalletHistory(platform, null);
                                },
                                (responseErc20) =>
                                {
                                    var ethHistory = new Dictionary<string, DateTime>();

                                    // Adding ETH transactions to the dict.
                                    if (responseEth != null)
                                    {
                                        var entries = responseEth.GetNode("result");
                                        foreach (var entry in entries.Children)
                                        {
                                            var hash = entry.GetString("hash");
                                            if (!ethHistory.Any(x => x.Key == hash))
                                            {
                                                ethHistory.Add(hash, new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(entry.GetUInt32("timeStamp")).ToLocalTime());
                                            }
                                        }
                                    }

                                    // Adding ERC20 transactions to the dict.
                                    if (responseErc20 != null)
                                    {
                                        var entries = responseErc20.GetNode("result");
                                        foreach (var entry in entries.Children)
                                        {
                                            var hash = entry.GetString("hash");
                                            if (!ethHistory.Any(x => x.Key == hash))
                                            {
                                                ethHistory.Add(hash, new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(entry.GetUInt32("timeStamp")).ToLocalTime());
                                            }
                                        }
                                    }

                                    // Sorting tx-es by date.
                                    var orderedEthHistory = ethHistory.OrderByDescending(x => x.Value);

                                    var history = new List<HistoryEntry>();

                                    foreach (var entry in orderedEthHistory)
                                    {
                                        history.Add(new HistoryEntry()
                                        {
                                            hash = entry.Key,
                                            date = entry.Value,
                                            url = GetEtherscanTransactionURL(entry.Key),
                                        });
                                    }

                                    ReportWalletHistory(platform, history);
                                }));
                            }));
                        }
                        break;

                    case PlatformKind.BSC:
                        {
                            var keys = EthereumKey.FromWIF(wif);
                            var urlBsc = GetBscExplorerAPIUrl($"module=account&action=txlist&address={keys.Address}&sort=desc");

                            StartCoroutine(WebClient.RESTRequest(urlBsc, WebClient.DefaultTimeout, (error, msg) =>
                            {
                                ReportWalletHistory(platform, null);
                            },
                            (responseBsc) =>
                            {
                                var urlBep20 = GetBscExplorerAPIUrl($"module=account&action=tokentx&address={keys.Address}&sort=desc");
                                StartCoroutine(WebClient.RESTRequest(urlBep20, WebClient.DefaultTimeout, (error, msg) =>
                                {
                                    ReportWalletHistory(platform, null);
                                },
                                (responseBep20) =>
                                {
                                    var bscHistory = new Dictionary<string, DateTime>();

                                    // Adding BSC transactions to the dict.
                                    if (responseBsc != null)
                                    {
                                        var entries = responseBsc.GetNode("result");
                                        foreach (var entry in entries.Children)
                                        {
                                            var hash = entry.GetString("hash");
                                            if (!bscHistory.Any(x => x.Key == hash))
                                            {
                                                bscHistory.Add(hash, new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(entry.GetUInt32("timeStamp")).ToLocalTime());
                                            }
                                        }
                                    }

                                    // Adding BEP20 transactions to the dict.
                                    if (responseBep20 != null)
                                    {
                                        var entries = responseBep20.GetNode("result");
                                        foreach (var entry in entries.Children)
                                        {
                                            var hash = entry.GetString("hash");
                                            if (!bscHistory.Any(x => x.Key == hash))
                                            {
                                                bscHistory.Add(hash, new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(entry.GetUInt32("timeStamp")).ToLocalTime());
                                            }
                                        }
                                    }

                                    // Sorting tx-es by date.
                                    var orderedBscHistory = bscHistory.OrderByDescending(x => x.Value);

                                    var history = new List<HistoryEntry>();

                                    foreach (var entry in orderedBscHistory)
                                    {
                                        history.Add(new HistoryEntry()
                                        {
                                            hash = entry.Key,
                                            date = entry.Value,
                                            url = GetBscTransactionURL(entry.Key),
                                        });
                                    }

                                    ReportWalletHistory(platform, history);
                                }));
                            }));
                        }
                        break;

                    default:
                        ReportWalletHistory(platform, null);
                        break;
                }
            }
        }

        public string GetPhantasmaTransactionURL(string hash)
        {
            var url = Settings.phantasmaExplorer;
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            return $"{url}tx/{hash}";
        }

        public string GetPhantasmaAddressURL(string address)
        {
            var url = Settings.phantasmaExplorer;
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            return $"{url}address/{address}";
        }

        public string GetPhantasmaContractURL(string symbol)
        {
            var url = Settings.phantasmaExplorer;
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            return $"{url}contract/{symbol}";
        }

        public string GetPhantasmaNftURL(string symbol, string tokenId)
        {
            var url = Settings.phantasmaNftExplorer;
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            return $"{url}{symbol.ToLower()}/{tokenId}";
        }

        private void RequestPendings(string address, PlatformKind platform, Action<Swap[], string> callback)
        {
            callback(Array.Empty<Swap>(), null);
            
            /*StartCoroutine(phantasmaApi.GetSwapsForAddress(address, platform.ToString().ToLower(), (swaps) =>
            {
                callback(swaps, null);
            }, (error, msg) =>
            {
                if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                {
                    ChangeFaultyRPCURL(PlatformKind.Phantasma);
                }
                callback(null, msg);
            }));*/
        }

        public string GetEtherscanTransactionURL(string hash)
        {
            if (!hash.StartsWith("0x"))
                hash = "0x" + hash;

            switch (Settings.ethereumNetwork)
            {
                case EthereumNetwork.Main_Net:
                    return $"https://etherscan.io/tx/{hash}";

                case EthereumNetwork.Ropsten:
                    return $"https://ropsten.etherscan.io/tx/{hash}";

                default:
                    return null;
            }
        }

        public string GetEtherscanAddressURL(string address)
        {
            if (!address.StartsWith("0x"))
                address = "0x" + address;

            switch (Settings.ethereumNetwork)
            {
                case EthereumNetwork.Main_Net:
                    return $"https://etherscan.io/address/{address}";

                case EthereumNetwork.Ropsten:
                    return $"https://ropsten.etherscan.io/address/{address}";

                default:
                    return null;
            }
        }

        private string GetEtherscanAPIUrl(string request)
        {
            if (string.IsNullOrEmpty(etherscanAPIToken))
            {
                return null;
            }

            switch (Settings.ethereumNetwork)
            {
                case EthereumNetwork.Main_Net:
                    return $"https://api.etherscan.io/api?apikey={etherscanAPIToken}&{request}";

                case EthereumNetwork.Ropsten:
                    return $"https://api-ropsten.etherscan.io/api?apikey={etherscanAPIToken}&{request}";

                default:
                    return null;
            }
        }

        public string GetBscTransactionURL(string hash)
        {
            if (!hash.StartsWith("0x"))
                hash = "0x" + hash;

            switch (Settings.binanceSmartChainNetwork)
            {
                case BinanceSmartChainNetwork.Main_Net:
                    return $"https://bscscan.com/tx/{hash}";

                case BinanceSmartChainNetwork.Test_Net:
                    return $"https://testnet.bscscan.com/tx/{hash}";

                default:
                    return null;
            }
        }

        public string GetBscAddressURL(string address)
        {
            if (!address.StartsWith("0x"))
                address = "0x" + address;

            switch (Settings.binanceSmartChainNetwork)
            {
                case BinanceSmartChainNetwork.Main_Net:
                    return $"https://bscscan.com/address/{address}";

                case BinanceSmartChainNetwork.Test_Net:
                    return $"https://testnet.bscscan.com/address/{address}";

                default:
                    return null;
            }
        }

        private string GetBscExplorerAPIUrl(string request)
        {
            switch (Settings.binanceSmartChainNetwork)
            {
                case BinanceSmartChainNetwork.Main_Net:
                    return $"https://api.bscscan.com/api?{request}";

                case BinanceSmartChainNetwork.Test_Net:
                    return $"https://testnet.bscscan.com/api?{request}";

                default:
                    return null;
            }
        }

        public string GetNeoscanTransactionURL(string hash)
        {
            var url = Settings.neoscanURL;
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            return $"{url}transaction/{hash}";
        }
        public string GetNeoscanAddressURL(string address)
        {
            var url = Settings.neoscanURL;
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            return $"{url}address/{address}";
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

        public int AddWallet(string name, string wif, string password, bool legacySeed)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 3)
            {
                throw new Exception("Name is too short.");
            }

            if (name.Length > 16)
            {
                throw new Exception("Name is too long.");
            }

            for (int i = 0; i < Accounts.Count(); i++)
            {
                if (Accounts[i].name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("An account with this name already exists.");
                }
            }

            var account = new Account() { name = name, platforms = AccountManager.MergeAvailablePlatforms(), misc = "" };

            // Initializing public addresses.
            var phaKeys = PhantasmaKeys.FromWIF(wif);
            account.phaAddress = phaKeys.Address.ToString();

            var neoKeys = NeoKeys.FromWIF(wif);
            account.neoAddress = neoKeys.Address.ToString();

            var ethereumAddressUtil = new Poltergeist.PhantasmaLegacy.Ethereum.Util.AddressUtil();
            account.ethAddress = ethereumAddressUtil.ConvertToChecksumAddress(EthereumKey.FromWIF(wif).Address);

            if (!String.IsNullOrEmpty(password))
            {
                account.passwordProtected = true;
                account.passwordIterations = PasswordIterations;

                // Encrypting WIF.
                GetPasswordHash(password, account.passwordIterations, out string salt, out string passwordHash);
                account.password = "";
                account.salt = salt;

                account.WIF = EncryptString(wif, passwordHash, out string iv);
                account.iv = iv;

                // Decrypting to ensure there are no exceptions.
                DecryptString(account.WIF, passwordHash, account.iv);
            }
            else
            {
                account.passwordProtected = false;
                account.WIF = wif;
            }

            account.misc = legacySeed ? "legacy-seed" : "";

            Accounts.Add(account);

            return Accounts.Count() - 1;
        }

        public static Address EncodeNeoAddress(string addressText)
        {
            Throw.If(!Poltergeist.PhantasmaLegacy.Neo2.NeoUtils.IsValidAddress(addressText), "invalid neo address");
            var scriptHash = addressText.Base58CheckDecode();

            var pubKey = new byte[33];
            ByteArrayUtils.CopyBytes(scriptHash, 0, pubKey, 0, scriptHash.Length);

            return Address.FromInterop(1/*NeoID*/, pubKey);
        }

        public static Address EncodeEthereumAddress(string addressText)
        {
            var ethereumAddressUtil = new Poltergeist.PhantasmaLegacy.Ethereum.Util.AddressUtil();

            Throw.If(!ethereumAddressUtil.IsValidEthereumAddressHexFormat(addressText), "invalid Ethereum address");

            if (addressText.StartsWith("0x"))
            {
                addressText = addressText.Substring(2);
            }

            var scriptHash = Poltergeist.PhantasmaLegacy.Ethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(addressText);

            var pubKey = new byte[33];
            ByteArrayUtils.CopyBytes(scriptHash, 0, pubKey, 0, scriptHash.Length);

            return Address.FromInterop(2/*Ethereum*/, pubKey);
        }
        public static Address EncodeBinanceSmartChainAddress(string addressText)
        {
            var ethereumAddressUtil = new Poltergeist.PhantasmaLegacy.Ethereum.Util.AddressUtil();

            Throw.If(!ethereumAddressUtil.IsValidEthereumAddressHexFormat(addressText), "invalid Ethereum address");

            if (addressText.StartsWith("0x"))
            {
                addressText = addressText.Substring(2);
            }

            var scriptHash = Poltergeist.PhantasmaLegacy.Ethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(addressText);

            var pubKey = new byte[33];
            ByteArrayUtils.CopyBytes(scriptHash, 0, pubKey, 0, scriptHash.Length);

            return Address.FromInterop(3/*BinanceSmartChain*/, pubKey);
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
                        Log.Write($"Got {interopAddress} interop address for {platformName} platform");
                        callback(interopAddress);
                        return;
                    }
                }

                callback(null);
            }, (error, msg) =>
            {
                if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                {
                    ChangeFaultyRPCURL(PlatformKind.Phantasma);
                }
                callback(null);
            }));
        }

        public string SearchInteropMapForAddress(PlatformKind platform)
        {
            if (_interopMap.ContainsKey(platform))
            {
                return _interopMap[platform];
            }

            return null;
        }

        public void ClearInteropMap()
        {
            _interopMap.Clear();
        }

        internal void SettleSwap(string sourcePlatform, string destPlatform, string symbol, string pendingHash, BigInteger phaGasPrice, BigInteger phaGasLimit, Action<Hash, string> callback)
        {
            var accountManager = AccountManager.Instance;

            if (sourcePlatform.ToLower() == PlatformKind.Ethereum.ToString().ToLower())
            {
                var wif = this.CurrentWif;
                var ethKeys = EthereumKey.FromWIF(wif);
                var phantasmaKeys = PhantasmaKeys.FromWIF(wif);
                var address = phantasmaKeys.Address;

                Hash ethTxHash = Hash.Parse(pendingHash);
                var transcodedAddress = Address.FromKey(ethKeys);

                var kcalBalance = accountManager._states[PlatformKind.Phantasma].GetAvailableAmount("KCAL");

                byte[] script;
                if (kcalBalance < 0.1m)
                {
                    // We swap some tokens into KCAL
                    script = ScriptUtils.BeginScript()
                        .CallContract("interop", "SettleTransaction", transcodedAddress, PlatformKind.Ethereum.ToString().ToLower(), PlatformKind.Ethereum.ToString().ToLower(), ethTxHash)
                        .CallContract("swap", "SwapFee", transcodedAddress, symbol, UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals))
                        .AllowGas(transcodedAddress, Address.Null)
                        .TransferBalance(symbol, transcodedAddress, phantasmaKeys.Address)
                        .SpendGas(transcodedAddress)
                        .EndScript();

                    SignAndSendTransaction("main", script, phaGasPrice, phaGasLimit, System.Text.Encoding.UTF8.GetBytes(WalletIdentifier), ProofOfWork.None, ethKeys, (hash, error) =>
                    {
                        callback(hash, error);
                    }, (message, prikey, pubkey) =>
                    {
                        return Poltergeist.PhantasmaLegacy.Cryptography.CryptoUtils.Sign(message, prikey, pubkey, ECDsaCurve.Secp256k1);
                    });
                }
                else
                {
                    // We use KCAL that is available on this account already
                    script = ScriptUtils.BeginScript()
                        .CallContract("interop", "SettleTransaction", transcodedAddress, PlatformKind.Ethereum.ToString().ToLower(), PlatformKind.Ethereum.ToString().ToLower(), ethTxHash)
                        .AllowGas(address, Address.Null)
                        .TransferBalance(symbol, transcodedAddress, phantasmaKeys.Address)
                        .SpendGas(address)
                        .EndScript();

                    SignAndSendTransaction("main", script, phaGasPrice, phaGasLimit, System.Text.Encoding.UTF8.GetBytes(WalletIdentifier), ProofOfWork.None, ethKeys, (hash, error) =>
                    {
                        callback(hash, error);
                    }, (message, prikey, pubkey) =>
                    {
                        return Poltergeist.PhantasmaLegacy.Cryptography.CryptoUtils.Sign(message, prikey, pubkey, ECDsaCurve.Secp256k1);
                    });
                }
            }
            else if (sourcePlatform.ToLower() == PlatformKind.BSC.ToString().ToLower())
            {
                var wif = this.CurrentWif;
                var ethKeys = EthereumKey.FromWIF(wif);
                var phantasmaKeys = PhantasmaKeys.FromWIF(wif);
                var address = phantasmaKeys.Address;

                Hash ethTxHash = Hash.Parse(pendingHash);
                var transcodedAddress = Address.FromKey(ethKeys);

                var kcalBalance = accountManager._states[PlatformKind.Phantasma].GetAvailableAmount("KCAL");

                byte[] script;
                if (kcalBalance < 0.1m)
                {
                    // We swap some tokens into KCAL
                    script = ScriptUtils.BeginScript()
                        .CallContract("interop", "SettleTransaction", transcodedAddress, PlatformKind.BSC.ToString().ToLower(), PlatformKind.BSC.ToString().ToLower(), ethTxHash)
                        .CallContract("swap", "SwapFee", transcodedAddress, symbol, UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals))
                        .AllowGas(transcodedAddress, Address.Null)
                        .TransferBalance(symbol, transcodedAddress, phantasmaKeys.Address)
                        .SpendGas(transcodedAddress)
                        .EndScript();

                    SignAndSendTransaction("main", script, phaGasPrice, phaGasLimit, System.Text.Encoding.UTF8.GetBytes(WalletIdentifier), ProofOfWork.None, ethKeys, (hash, error) =>
                    {
                        callback(hash, error);
                    }, (message, prikey, pubkey) =>
                    {
                        return Poltergeist.PhantasmaLegacy.Cryptography.CryptoUtils.Sign(message, prikey, pubkey, ECDsaCurve.Secp256k1);
                    });
                }
                else
                {
                    // We use KCAL that is available on this account already
                    script = ScriptUtils.BeginScript()
                        .CallContract("interop", "SettleTransaction", transcodedAddress, PlatformKind.BSC.ToString().ToLower(), PlatformKind.BSC.ToString().ToLower(), ethTxHash)
                        .AllowGas(address, Address.Null)
                        .TransferBalance(symbol, transcodedAddress, phantasmaKeys.Address)
                        .SpendGas(address)
                        .EndScript();

                    SignAndSendTransaction("main", script, phaGasPrice, phaGasLimit, System.Text.Encoding.UTF8.GetBytes(WalletIdentifier), ProofOfWork.None, ethKeys, (hash, error) =>
                    {
                        callback(hash, error);
                    }, (message, prikey, pubkey) =>
                    {
                        return Poltergeist.PhantasmaLegacy.Cryptography.CryptoUtils.Sign(message, prikey, pubkey, ECDsaCurve.Secp256k1);
                    });
                }
            }
            else
            {
                StartCoroutine(phantasmaApi.SettleSwap(sourcePlatform, destPlatform, pendingHash, (hash) =>
                {
                    callback(Hash.Parse(hash), null);
                }, (error, msg) =>
                {
                    if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                    {
                        ChangeFaultyRPCURL(PlatformKind.Phantasma);
                    }
                    Log.WriteWarning(msg);
                    callback(Hash.Null, msg);
                }));
            }
        }

        internal void DeleteAccount(int currentIndex)
        {
            if (currentIndex<0 || currentIndex >= Accounts.Count())
            {
                return;
            }

            Accounts.RemoveAt(currentIndex);
            SaveAccounts();
        }

        internal void ReplaceAccountWIF(int currentIndex, string wif, string passwordHash, out string deletedDuplicateWallet)
        {
            deletedDuplicateWallet = null;

            if (currentIndex < 0 || currentIndex >= Accounts.Count())
            {
                return;
            }

            var account = Accounts[currentIndex];
            if (string.IsNullOrEmpty(passwordHash))
            {
                account.WIF = wif;
            }
            else
            {
                account.WIF = EncryptString(wif, passwordHash, out string iv);
                account.iv = iv;
            }
            account.misc = ""; // Migration does not guarantee that new account have current seed, but that's all that we can do with it.
            
            // Initializing new public addresses.
            wif = account.GetWif(passwordHash); // Recreating to be sure all is good.
            var phaKeys = PhantasmaKeys.FromWIF(wif);
            account.phaAddress = phaKeys.Address.ToString();

            var neoKeys = NeoKeys.FromWIF(wif);
            account.neoAddress = neoKeys.Address.ToString();

            var ethereumAddressUtil = new Poltergeist.PhantasmaLegacy.Ethereum.Util.AddressUtil();
            account.ethAddress = ethereumAddressUtil.ConvertToChecksumAddress(EthereumKey.FromWIF(wif).Address);

            Accounts[currentIndex] = account;

            for(var i = 0; i < Accounts.Count; i++)
            {
                if(i != currentIndex && Accounts[i].phaAddress == account.phaAddress)
                {
                    deletedDuplicateWallet = Accounts[i].name;
                    Accounts.RemoveAt(i);
                    break;
                }
            }

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

            var account2 = Accounts[CurrentIndex];
            account2.name = newName;
            Accounts[CurrentIndex] = account2;
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
                    if (error == EPHANTASMA_SDK_ERROR_TYPE.WEB_REQUEST_ERROR)
                    {
                        ChangeFaultyRPCURL(PlatformKind.Phantasma);
                    }
                    callback(null);
                })
            );
        }

        public string GetAddress(int index, PlatformKind platform)
        {
            if (index < 0 || index >= Accounts.Count())
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

            switch (platform)
            {
                case PlatformKind.Phantasma:
                    return Accounts[index].phaAddress;

                case PlatformKind.Neo:
                    return Accounts[index].neoAddress;

                case PlatformKind.Ethereum:
                    return Accounts[index].ethAddress;

                case PlatformKind.BSC:
                    return Accounts[index].ethAddress;
            }

            return null;
        }

        public void ResetNftsSorting()
        {
            currentTtrsNftsSortMode = TtrsNftSortMode.None;
            currentNftsSortMode = NftSortMode.None;
            currentNftsSortDirection = SortDirection.None;
        }

        public void SortTtrsNfts(string symbol)
        {
            if (_nfts[CurrentPlatform] == null)
                return;

            if (!nftDescriptionsAreFullyLoaded) // We should not sort NFTs if there are no attributes available.
                return;

            if (symbol == "TTRS")
            {
                if (currentTtrsNftsSortMode == (TtrsNftSortMode)Settings.ttrsNftSortMode && (int)currentNftsSortDirection == Settings.nftSortDirection)
                    return; // Nothing changed, no need to sort again.

                switch ((TtrsNftSortMode)Settings.ttrsNftSortMode)
                {
                    case TtrsNftSortMode.Number_Date:
                        if (Settings.nftSortDirection == (int)SortDirection.Ascending)
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderBy(x => TtrsStore.GetNft(x.ID).Mint).ThenBy(x => TtrsStore.GetNft(x.ID).Timestamp).ToList();
                        else
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderByDescending(x => TtrsStore.GetNft(x.ID).Mint).ThenByDescending(x => TtrsStore.GetNft(x.ID).Timestamp).ToList();
                        break;
                    case TtrsNftSortMode.Date_Number:
                        if (Settings.nftSortDirection == (int)SortDirection.Ascending)
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderBy(x => TtrsStore.GetNft(x.ID).Timestamp).ThenBy(x => TtrsStore.GetNft(x.ID).Mint).ToList();
                        else
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderByDescending(x => TtrsStore.GetNft(x.ID).Timestamp).ThenByDescending(x => TtrsStore.GetNft(x.ID).Mint).ToList();
                        break;
                    case TtrsNftSortMode.Type_Number_Date:
                        if (Settings.nftSortDirection == (int)SortDirection.Ascending)
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderByDescending(x => TtrsStore.GetNft(x.ID).Type).ThenBy(x => TtrsStore.GetNft(x.ID).Mint).ThenBy(x => TtrsStore.GetNft(x.ID).Timestamp).ToList();
                        else
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderBy(x => TtrsStore.GetNft(x.ID).Type).ThenByDescending(x => TtrsStore.GetNft(x.ID).Mint).ThenByDescending(x => TtrsStore.GetNft(x.ID).Timestamp).ToList();
                        break;
                    case TtrsNftSortMode.Type_Date_Number:
                        if (Settings.nftSortDirection == (int)SortDirection.Ascending)
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderByDescending(x => TtrsStore.GetNft(x.ID).Type).ThenBy(x => TtrsStore.GetNft(x.ID).Timestamp).ThenBy(x => TtrsStore.GetNft(x.ID).Mint).ToList();
                        else
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderBy(x => TtrsStore.GetNft(x.ID).Type).ThenByDescending(x => TtrsStore.GetNft(x.ID).Timestamp).ThenByDescending(x => TtrsStore.GetNft(x.ID).Mint).ToList();
                        break;
                    case TtrsNftSortMode.Type_Rarity: // And also Number and Date as last sorting parameters.
                        if (Settings.nftSortDirection == (int)SortDirection.Ascending)
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderByDescending(x => TtrsStore.GetNft(x.ID).Type).ThenByDescending(x => TtrsStore.GetNft(x.ID).Rarity).ThenBy(x => TtrsStore.GetNft(x.ID).Mint).ThenBy(x => TtrsStore.GetNft(x.ID).Timestamp).ToList();
                        else
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderBy(x => TtrsStore.GetNft(x.ID).Type).ThenBy(x => TtrsStore.GetNft(x.ID).Rarity).ThenByDescending(x => TtrsStore.GetNft(x.ID).Mint).ThenByDescending(x => TtrsStore.GetNft(x.ID).Timestamp).ToList();
                        break;
                }

                currentTtrsNftsSortMode = (TtrsNftSortMode)Settings.ttrsNftSortMode;
            }
            else if (symbol == "GAME")
            {
                if (currentNftsSortMode == (NftSortMode)Settings.nftSortMode && (int)currentNftsSortDirection == Settings.nftSortDirection)
                    return; // Nothing changed, no need to sort again.

                switch ((NftSortMode)Settings.nftSortMode)
                {
                    case NftSortMode.Name:
                        if (Settings.nftSortDirection == (int)SortDirection.Ascending)
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderBy(x => GameStore.GetNft(x.ID).name_english).ToList();
                        else
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderByDescending(x => GameStore.GetNft(x.ID).name_english).ToList();
                        break;
                    case NftSortMode.Number_Date:
                        if (Settings.nftSortDirection == (int)SortDirection.Ascending)
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderBy(x => GameStore.GetNft(x.ID).mint).ThenBy(x => GameStore.GetNft(x.ID).timestampDT).ToList();
                        else
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderByDescending(x => GameStore.GetNft(x.ID).mint).ThenByDescending(x => GameStore.GetNft(x.ID).timestampDT).ToList();
                        break;
                    case NftSortMode.Date_Number:
                        if (Settings.nftSortDirection == (int)SortDirection.Ascending)
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderBy(x => GameStore.GetNft(x.ID).timestampDT).ThenBy(x => GameStore.GetNft(x.ID).mint).ToList();
                        else
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderByDescending(x => GameStore.GetNft(x.ID).timestampDT).ThenByDescending(x => GameStore.GetNft(x.ID).mint).ToList();
                        break;
                }

                currentNftsSortMode = (NftSortMode)Settings.nftSortMode;
            }
            else
            {
                if (currentNftsSortMode == (NftSortMode)Settings.nftSortMode && (int)currentNftsSortDirection == Settings.nftSortDirection)
                    return; // Nothing changed, no need to sort again.

                switch ((NftSortMode)Settings.nftSortMode)
                {
                    case NftSortMode.Name:
                        if (Settings.nftSortDirection == (int)SortDirection.Ascending)
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderBy(x => GetNft(x.ID).parsedRom.GetName()).ToList();
                        else
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderByDescending(x => GetNft(x.ID).parsedRom.GetName()).ToList();
                        break;
                    case NftSortMode.Number_Date:
                        if (Settings.nftSortDirection == (int)SortDirection.Ascending)
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderBy(x => GetNft(x.ID).mint).ThenBy(x => GetNft(x.ID).parsedRom.GetDate()).ToList();
                        else
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderByDescending(x => GetNft(x.ID).mint).ThenByDescending(x => GetNft(x.ID).parsedRom.GetDate()).ToList();
                        break;
                    case NftSortMode.Date_Number:
                        if (Settings.nftSortDirection == (int)SortDirection.Ascending)
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderBy(x => GetNft(x.ID).parsedRom.GetDate()).ThenBy(x => GetNft(x.ID).mint).ToList();
                        else
                            _nfts[CurrentPlatform] = _nfts[CurrentPlatform].OrderByDescending(x => GetNft(x.ID).parsedRom.GetDate()).ThenByDescending(x => GetNft(x.ID).mint).ToList();
                        break;
                }

                currentNftsSortMode = (NftSortMode)Settings.nftSortMode;
            }
            
            currentNftsSortDirection = (SortDirection)Settings.nftSortDirection;
        }

        public decimal CalculateRequireStakeForStorage(int totalSize)
        {
            var kilobytesPerStake = 39; // TODO this should be governance value obtained from chain
            var stakeAmount = (totalSize * UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals))  / (kilobytesPerStake * 1024);
            return UnitConversion.ToDecimal(stakeAmount, DomainSettings.StakingTokenDecimals);
        }

        public TokenData GetNft(string id)
        {
            return _nfts[CurrentPlatform].Where(x => x.ID == id).FirstOrDefault();
        }

        public void GetPhantasmaAddressInfo(Action<string, string> callback)
        {
            GetPhantasmaAddressInfo(_states[PlatformKind.Phantasma].address, callback);
        }
        public void GetPhantasmaAddressInfo(string addressString, Action<string, string> callback)
        {
            byte[] scriptUnclaimed;
            byte[] scriptStake;
            byte[] scriptStorageStake;
            byte[] scriptVotingPower;
            byte[] scriptStakeTimestamp;
            byte[] scriptTimeBeforeUnstake;
            byte[] scriptMasterDate;
            byte[] scriptIsMaster;
            try
            {
                var address = Address.FromText(addressString);

                {
                    var sb = new ScriptBuilder();
                    sb.CallContract("stake", "GetUnclaimed", address);
                    scriptUnclaimed = sb.EndScript();
                }
                {
                    var sb = new ScriptBuilder();
                    sb.CallContract("stake", "GetStake", address);
                    scriptStake = sb.EndScript();
                }
                {
                    var sb = new ScriptBuilder();
                    sb.CallContract("stake", "GetStorageStake", address);
                    scriptStorageStake = sb.EndScript();
                }
                {
                    var sb = new ScriptBuilder();
                    sb.CallContract("stake", "GetAddressVotingPower", address);
                    scriptVotingPower = sb.EndScript();
                }
                {
                    var sb = new ScriptBuilder();
                    sb.CallContract("stake", "GetStakeTimestamp", address);
                    scriptStakeTimestamp = sb.EndScript();
                }
                {
                    var sb = new ScriptBuilder();
                    sb.CallContract("stake", "GetTimeBeforeUnstake", address);
                    scriptTimeBeforeUnstake = sb.EndScript();
                }
                {
                    var sb = new ScriptBuilder();
                    sb.CallContract("stake", "GetMasterDate", address);
                    scriptMasterDate = sb.EndScript();
                }
                {
                    var sb = new ScriptBuilder();
                    sb.CallContract("stake", "IsMaster", address);
                    scriptIsMaster = sb.EndScript();
                }
            }
            catch (Exception e)
            {
                callback(null, e.ToString());
                return;
            }

            InvokeScriptPhantasma("main", scriptUnclaimed, (unclaimedResult, unclaimedInvokeError) =>
            {
                if (!string.IsNullOrEmpty(unclaimedInvokeError))
                {
                    callback(null, "Script invokation error!\n\n" + unclaimedInvokeError);
                    return;
                }
                else
                {
                    InvokeScriptPhantasma("main", scriptStake, (stakeResult, stakeInvokeError) =>
                    {
                        if (!string.IsNullOrEmpty(stakeInvokeError))
                        {
                            callback(null, "Script invokation error!\n\n" + stakeInvokeError);
                            return;
                        }
                        else
                        {
                            InvokeScriptPhantasma("main", scriptStorageStake, (storageStakeResult, storageStakeInvokeError) =>
                            {
                                if (!string.IsNullOrEmpty(storageStakeInvokeError))
                                {
                                    callback(null, "Script invokation error!\n\n" + storageStakeInvokeError);
                                    return;
                                }
                                else
                                {
                                    InvokeScriptPhantasma("main", scriptVotingPower, (votingPowerResult, votingPowerInvokeError) =>
                                    {
                                        if (!string.IsNullOrEmpty(votingPowerInvokeError))
                                        {
                                            callback(null, "Script invokation error!\n\n" + votingPowerInvokeError);
                                            return;
                                        }
                                        else
                                        {
                                            InvokeScriptPhantasma("main", scriptStakeTimestamp, (stakeTimestampResult, stakeTimestampInvokeError) =>
                                            {
                                                if (!string.IsNullOrEmpty(stakeTimestampInvokeError))
                                                {
                                                    callback(null, "Script invokation error!\n\n" + stakeTimestampInvokeError);
                                                    return;
                                                }
                                                else
                                                {
                                                    InvokeScriptPhantasma("main", scriptTimeBeforeUnstake, (timeBeforeUnstakeResult, timeBeforeUnstakeInvokeError) =>
                                                    {
                                                        if (!string.IsNullOrEmpty(timeBeforeUnstakeInvokeError))
                                                        {
                                                            callback(null, "Script invokation error!\n\n" + timeBeforeUnstakeInvokeError);
                                                            return;
                                                        }
                                                        else
                                                        {
                                                            InvokeScriptPhantasma("main", scriptMasterDate, (masterDateResult, masterDateInvokeError) =>
                                                            {
                                                                if (!string.IsNullOrEmpty(masterDateInvokeError))
                                                                {
                                                                    callback(null, "Script invokation error!\n\n" + masterDateInvokeError);
                                                                    return;
                                                                }
                                                                else
                                                                {
                                                                    InvokeScriptPhantasma("main", scriptIsMaster, (isMasterResult, isMasterInvokeError) =>
                                                                    {
                                                                    if (!string.IsNullOrEmpty(isMasterInvokeError))
                                                                    {
                                                                        callback(null, "Script invokation error!\n\n" + isMasterInvokeError);
                                                                        return;
                                                                    }
                                                                    else
                                                                    {
                                                                            StartCoroutine(phantasmaApi.GetAllAddressTransactions(addressString, (x) =>
                                                                            {
                                                                                var stakingHistory = new List<string>();
                                                                                var calculatedStakedAmount = 0m;
                                                                                foreach (var tx in x.txs.Reverse().Where(t => t.events.Any(e => (e.address == addressString && (e.kind.ToUpper() == "TOKENSTAKE" || e.kind.ToUpper() == "TOKENCLAIM") && e.contract.ToUpper() == "STAKE") && !t.events.Any(e2 => e2.kind.ToUpper() == "TOKENMINT" && e2.address == "S3dP2jjf1jUG9nethZBWbnu9a6dFqB7KveTWU7znis6jpDy")) || t.events.Any(e => e.kind.ToUpper() == "ADDRESSMIGRATION")))
                                                                                {
                                                                                    var stakeEvent = tx.events.Where(e => (e.address == addressString && (e.kind.ToUpper() == "TOKENSTAKE" || e.kind.ToUpper() == "TOKENCLAIM") && e.contract.ToUpper() == "STAKE") || e.kind.ToUpper() == "ADDRESSMIGRATION").First();

                                                                                    var kind = (EventKind)Enum.Parse(typeof(EventKind), stakeEvent.kind);
                                                                                    if (kind == EventKind.AddressMigration)
                                                                                    {
                                                                                        Log.Write(new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(tx.timestamp).ToLocalTime() +
                                                                                            ": " + stakeEvent.kind + ": " + " address: " + stakeEvent.address + " data: " + stakeEvent.data + " tx: " + tx.hash);

                                                                                        stakingHistory.Add(new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(tx.timestamp).ToLocalTime() +
                                                                                            ": Account migration to address: " + stakeEvent.address);
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        var evnt = new Phantasma.Core.Domain.Event(kind, Address.FromText(stakeEvent.address), stakeEvent.contract, Base16.Decode(stakeEvent.data));

                                                                                        var tokenEventData = evnt.GetContent<TokenEventData>();
                                                                                        Log.Write(new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(tx.timestamp).ToLocalTime() +
                                                                                            ": " + stakeEvent.kind + ": " + UnitConversion.ToDecimal(tokenEventData.Value, 8) + " " + tokenEventData.Symbol + " address: " + stakeEvent.address + " data: " + stakeEvent.data + " tx: " + tx.hash);

                                                                                        var soulAmount = UnitConversion.ToDecimal(tokenEventData.Value, 8);
                                                                                        if (kind == EventKind.TokenStake)
                                                                                            calculatedStakedAmount += soulAmount;
                                                                                        else
                                                                                            calculatedStakedAmount -= soulAmount;

                                                                                        stakingHistory.Add(new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(tx.timestamp).ToLocalTime() +
                                                                                            ": " + (kind == EventKind.TokenStake ? "Stake" : "Claim") + ": " + soulAmount + " " + tokenEventData.Symbol + $" [Staked: {calculatedStakedAmount} SOUL]");
                                                                                    }
                                                                                }

                                                                                var unclaimed = UnitConversion.ToDecimal(VMObject.FromBytes(unclaimedResult).AsNumber(), 10);
                                                                                var stake = UnitConversion.ToDecimal(VMObject.FromBytes(stakeResult).AsNumber(), 8);
                                                                                var storageStake = UnitConversion.ToDecimal(VMObject.FromBytes(storageStakeResult).AsNumber(), 8);
                                                                                var votingPower = VMObject.FromBytes(votingPowerResult).AsNumber();
                                                                                var stakeTimestamp = VMObject.FromBytes(stakeTimestampResult).AsTimestamp();
                                                                                var stakeTimestampLocal = ((DateTime)stakeTimestamp).ToLocalTime();
                                                                                var timeBeforeUnstake = VMObject.FromBytes(timeBeforeUnstakeResult).AsNumber();
                                                                                var masterDate = VMObject.FromBytes(masterDateResult).AsTimestamp();
                                                                                var isMaster = VMObject.FromBytes(isMasterResult).AsBool();

                                                                                stakingHistory.Reverse();

                                                                                callback($"{addressString} account information:\n\n" +
                                                                                    $"Unclaimed: {unclaimed} KCAL\n" +
                                                                                    $"Stake: {stake} SOUL\n" +
                                                                                    $"Is SM: {isMaster}\n" +
                                                                                    $"SM since: {masterDate}\n" +
                                                                                    $"Stake timestamp: {stakeTimestampLocal} ({stakeTimestamp} UTC)\n" +
                                                                                    $"Next staking period starts in: {TimeSpan.FromSeconds((double)timeBeforeUnstake):hh\\:mm\\:ss}\n" +
                                                                                    $"Storage stake: {storageStake} SOUL\n" +
                                                                                    $"Voting power: {votingPower}\n\n" +
                                                                                    $"Staking history (calculated current stake: {calculatedStakedAmount} SOUL):\n{string.Join("\n", stakingHistory)}", null);
                                                                            }));
                                                                        }
                                                                    });
                                                                }
                                                            });
                                                        }
                                                    });
                                                }
                                            });
                                        }
                                    });
                                }
                            });
                        }
                    });
                }

            });
        }
    }
}
