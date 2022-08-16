using Phantasma.Numerics;
using Phantasma.SDK;
using System;
using UnityEngine;

namespace Poltergeist
{
    public enum NexusKind
    {
        Unknown,
        Main_Net,
        Test_Net,
        Mankini_Test_Net,
        Local_Net,
        Custom
    }

    public enum EthereumNetwork
    {
        Unknown,
        Main_Net,
        Ropsten,
        Local_Net
    }

    public enum BinanceSmartChainNetwork
    {
        Unknown,
        Main_Net,
        Test_Net,
        Local_Net
    }

    public enum UiThemes
    {
        Classic,
        Phantasia
    }

    public enum MnemonicPhraseLength
    {
        Twelve_Words,
        Twenty_Four_Words
    }

    public enum PasswordMode
    {
        Ask_Always,
        Ask_Only_On_Login,
        Master_Password
    }

    public enum MnemonicPhraseVerificationMode
    {
        Full,
        Simplified
    }

    public static class SettingsExtension
    {
        public static bool IsValidURL(this string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            if (!(url.StartsWith("http://") || url.StartsWith("https://")))
            {
                return false;
            }

            return true;
        }
    }

    public class Settings
    {
        public const string PhantasmaRPCTag = "settings.phantasma.rpc.url";
        public const string PhantasmaExplorerTag = "settings.phantasma.explorer.url";
        public const string PhantasmaNftExplorerTag = "settings.phantasma.nft.explorer.url";
        public const string NeoRPCTag = "settings.neo.rpc.url";
        public const string NeoscanAPITag = "settings.neoscan.url";
        public const string NexusNameTag = "settings.nexus.name";

        public const string NexusKindTag = "settings.nexus.kind";
        public const string CurrencyTag = "settings.currency";
        public const string GasPriceTag = "settings.fee.price";
        public const string GasLimitTag = "settings.fee.limit";

        public const string NeoGasFeeTag = "settings.neo.gas.fee";
        public const string NeoUserTokensTag = "settings.neo.user.tokens";

        public const string EthereumNetworkTag = "settings.ethereum.network";
        public const string EthereumRPCTag = "settings.ethereum.rpc.url";
        public const string EthereumGasPriceGweiTag = "settings.ethereum.gas.price.gwei";
        public const string EthereumTransferGasLimitTag = "settings.ethereum.transfer.gas.limit";
        public const string EthereumTokenTransferGasLimitTag = "settings.ethereum.token.transfer.gas.limit";
        public const string EthereumUserTokensTag = "settings.ethereum.user.tokens";

        public const string BinanceSmartChainNetworkTag = "settings.binancesmartchain.network";
        public const string BinanceSmartChainRPCTag = "settings.binancesmartchain.rpc.url";
        public const string BinanceSmartChainGasPriceGweiTag = "settings.binancesmartchain.gas.price.gwei";
        public const string BinanceSmartChainTransferGasLimitTag = "settings.binancesmartchain.transfer.gas.limit";
        public const string BinanceSmartChainTokenTransferGasLimitTag = "settings.binancesmartchain.token.transfer.gas.limit";
        public const string BinanceSmartChainUserTokensTag = "settings.binancesmartchain.user.tokens";

        public const string SFXTag = "settings.sfx";

        public const string LogLevelTag = "log.level";
        public const string LogOverwriteModeTag = "log.overwrite.mode";

        public const string UiThemeNameTag = "ui.theme.name";

        public const string TtrsNftSortModeTag = "ttrs.nft.sort.mode";
        public const string NftSortModeTag = "nft.sort.mode";
        public const string NftSortDirectionTag = "nft.sort.direction";

        public const string LastVisitedFolderTag = "last.visited.folder";

        public const string MnemonicPhraseLengthTag = "mnemonic.phrase.length";
        public const string MnemonicPhraseVerificationModeTag = "mnemonic.phrase.verification.mode";

        public const string PasswordModeTag = "password.mode";

        public string phantasmaRPCURL;
        public string phantasmaExplorer;
        public string phantasmaNftExplorer;
        public string neoRPCURL;
        public string neoscanURL;
        public string nexusName;
        public string currency;
        public BigInteger feePrice;
        public BigInteger feeLimit;
        public decimal neoGasFee;
        public string neoUserTokens;
        public EthereumNetwork ethereumNetwork;
        public string ethereumRPCURL;
        public BigInteger ethereumGasPriceGwei;
        public BigInteger ethereumTransferGasLimit;
        public BigInteger ethereumTokenTransferGasLimit;
        public string ethereumUserTokens;
        public BinanceSmartChainNetwork binanceSmartChainNetwork;
        public string binanceSmartChainRPCURL;
        public BigInteger binanceSmartChainGasPriceGwei;
        public BigInteger binanceSmartChainTransferGasLimit;
        public BigInteger binanceSmartChainTokenTransferGasLimit;
        public string binanceSmartChainUserTokens;
        public NexusKind nexusKind;
        public bool sfx;
        public Log.Level logLevel;
        public bool logOverwriteMode;
        public string uiThemeName;
        public int ttrsNftSortMode;
        public int nftSortMode;
        public int nftSortDirection;
        public string lastVisitedFolder;
        public MnemonicPhraseLength mnemonicPhraseLength;
        public MnemonicPhraseVerificationMode mnemonicPhraseVerificationMode;
        public PasswordMode passwordMode;

        public override string ToString()
        {
            return "Nexus kind: " + this.nexusKind.ToString() + "\n" +
                "Phantasma RPC: " + this.phantasmaRPCURL + "\n" +
                "Phantasma Explorer: " + this.phantasmaExplorer + "\n" +
                "Phantasma NFT Explorer: " + this.phantasmaNftExplorer + "\n" +
                "Fee price: " + this.feePrice + "\n" +
                "Fee limit: " + this.feeLimit + "\n" +
                "Neo RPC: " + this.neoRPCURL + "\n" +
                "Neoscan: " + this.neoscanURL + "\n" +
                "Neo GAS fee: " + this.neoGasFee + "\n" +
                "Neo user tokens: " + this.neoUserTokens + "\n" +
                "Ethereum network: " + this.ethereumNetwork + "\n" +
                "Ethereum RPC: " + this.ethereumRPCURL + "\n" +
                "Ethereum gas price (Gwei): " + this.ethereumGasPriceGwei + "\n" +
                "Ethereum transfer gas limit: " + this.ethereumTransferGasLimit + "\n" +
                "Ethereum token transfer gas limit: " + this.ethereumTokenTransferGasLimit + "\n" +
                "Ethereum user tokens: " + this.ethereumUserTokens + "\n" +
                "BinanceSmartChain network: " + this.binanceSmartChainNetwork + "\n" +
                "BinanceSmartChain RPC: " + this.binanceSmartChainRPCURL + "\n" +
                "BinanceSmartChain gas price (Gwei): " + this.binanceSmartChainGasPriceGwei + "\n" +
                "BinanceSmartChain transfer gas limit: " + this.binanceSmartChainTransferGasLimit + "\n" +
                "BinanceSmartChain token transfer gas limit: " + this.binanceSmartChainTokenTransferGasLimit + "\n" +
                "BinanceSmartChain user tokens: " + this.binanceSmartChainUserTokens + "\n" +
                "Nexus name: " + this.nexusName + "\n" +
                "Currency: " + this.currency + "\n" +
                "Sfx: " + this.sfx + "\n" +
                "UI theme: " + this.uiThemeName + "\n" +
                "Log level: " + this.logLevel + "\n" +
                "Log overwrite: " + this.logOverwriteMode + "\n" +
                "TTRS NFT sort mode: " + this.ttrsNftSortMode + "\n" +
                "NFT sort mode: " + this.nftSortMode + "\n" +
                "NFT sort direction: " + this.nftSortDirection + "\n" +
                "Mnemonic phrase length: " + this.mnemonicPhraseLength + "\n" +
                "Mnemonic phrase verification mode: " + this.mnemonicPhraseVerificationMode + "\n" +
                "Password mode: " + this.passwordMode;
        }

        public void LoadLogSettings()
        {
            var logLevel = PlayerPrefs.GetString(LogLevelTag, Log.Level.Networking.ToString());
            if (!Enum.TryParse<Log.Level>(logLevel, true, out this.logLevel))
            {
                this.logLevel = Log.Level.Networking;
            }

            this.logOverwriteMode = PlayerPrefs.GetInt(LogOverwriteModeTag, 1) != 0;
        }

        public void Load()
        {
            Log.Write("Settings: Loading...");

            var nexusKind = PlayerPrefs.GetString(NexusKindTag, NexusKind.Main_Net.ToString());
            if (!Enum.TryParse<NexusKind>(nexusKind, true, out this.nexusKind))
            {
                this.nexusKind = NexusKind.Unknown;
            }

            if (this.nexusKind == NexusKind.Main_Net || this.nexusKind == NexusKind.Test_Net || this.nexusKind == NexusKind.Mankini_Test_Net)
            {
                // For mainnet/testnet we always load defaults for hidden settings,
                // to avoid dealing with "stuck" values from old PG version that had different defaults.
                if (this.nexusKind == NexusKind.Main_Net)
                {
                    this.phantasmaRPCURL = PlayerPrefs.GetString(PhantasmaRPCTag, GetDefaultValue(PhantasmaRPCTag));
                }
                else
                {
                    // We cannot do it for mainnet, because for mainnet we store best RPC here.
                    // For testnets we should update with default value.
                    this.phantasmaRPCURL = GetDefaultValue(PhantasmaRPCTag);
                }
                this.phantasmaExplorer = GetDefaultValue(PhantasmaExplorerTag);
                this.phantasmaNftExplorer = GetDefaultValue(PhantasmaNftExplorerTag);
                this.neoRPCURL = GetDefaultValue(NeoRPCTag);
                this.neoscanURL = GetDefaultValue(NeoscanAPITag);
                this.nexusName = GetDefaultValue(NexusNameTag);
            }
            else
            {
                this.phantasmaRPCURL = PlayerPrefs.GetString(PhantasmaRPCTag, GetDefaultValue(PhantasmaRPCTag));
                this.phantasmaExplorer = PlayerPrefs.GetString(PhantasmaExplorerTag, GetDefaultValue(PhantasmaExplorerTag));
                this.phantasmaNftExplorer = PlayerPrefs.GetString(PhantasmaNftExplorerTag, GetDefaultValue(PhantasmaNftExplorerTag));
                this.neoRPCURL = PlayerPrefs.GetString(NeoRPCTag, GetDefaultValue(NeoRPCTag));
                this.neoscanURL = PlayerPrefs.GetString(NeoscanAPITag, GetDefaultValue(NeoscanAPITag));
                this.nexusName = PlayerPrefs.GetString(NexusNameTag, GetDefaultValue(NexusNameTag));
            }

            this.currency = PlayerPrefs.GetString(CurrencyTag, "USD");
            this.sfx = PlayerPrefs.GetInt(SFXTag, 0)!=0;

            var defaultGasPrice = 100000;
            if (!BigInteger.TryParse(PlayerPrefs.GetString(GasPriceTag, defaultGasPrice.ToString()), out feePrice))
            {
                this.feePrice = defaultGasPrice;
            }

            var defaultGasLimit = 2100;
            if (!BigInteger.TryParse(PlayerPrefs.GetString(GasLimitTag, defaultGasLimit.ToString()), out feeLimit))
            {
                this.feeLimit = defaultGasLimit;
            }

            // Doing it in a bit more complex way to avoid decimal parsing problem for different cultures.
            var neoGasFeeString = PlayerPrefs.GetString(NeoGasFeeTag, null);
            if (!String.IsNullOrEmpty(neoGasFeeString))
            {
                if (!Decimal.TryParse(neoGasFeeString, out neoGasFee))
                {
                    this.neoGasFee = 0.001m;
                }
            }
            else
                this.neoGasFee = 0.001m;

            neoUserTokens = PlayerPrefs.GetString(NeoUserTokensTag, null);

            // Ethereum
            var ethereumNetwork = PlayerPrefs.GetString(EthereumNetworkTag, EthereumNetwork.Main_Net.ToString());
            if (!Enum.TryParse<EthereumNetwork>(ethereumNetwork, true, out this.ethereumNetwork))
            {
                this.ethereumNetwork = EthereumNetwork.Unknown;
            }

            if (this.ethereumNetwork == EthereumNetwork.Main_Net || this.ethereumNetwork == EthereumNetwork.Ropsten)
            {
                // For mainnet/testnet we always load defaults for hidden settings,
                // to avoid dealing with "stuck" values from old PG version that had different defaults.
                this.ethereumRPCURL = GetDefaultValue(EthereumRPCTag);
            }
            else
            {
                this.ethereumRPCURL = PlayerPrefs.GetString(EthereumRPCTag, GetDefaultValue(EthereumRPCTag));
            }

            if (!BigInteger.TryParse(PlayerPrefs.GetString(EthereumGasPriceGweiTag, "100"), out ethereumGasPriceGwei))
            {
                this.ethereumGasPriceGwei = 100;
            }
            if (!BigInteger.TryParse(PlayerPrefs.GetString(EthereumTransferGasLimitTag, "21000"), out ethereumTransferGasLimit))
            {
                this.ethereumTransferGasLimit = 21000;
            }
            if (!BigInteger.TryParse(PlayerPrefs.GetString(EthereumTokenTransferGasLimitTag, "100000"), out ethereumTokenTransferGasLimit))
            {
                this.ethereumTokenTransferGasLimit = 100000;
            }

            ethereumUserTokens = PlayerPrefs.GetString(EthereumUserTokensTag, null);

            // BinanceSmartChain
            var binanceSmartChainNetwork = PlayerPrefs.GetString(BinanceSmartChainNetworkTag, BinanceSmartChainNetwork.Main_Net.ToString());
            if (!Enum.TryParse<BinanceSmartChainNetwork>(binanceSmartChainNetwork, true, out this.binanceSmartChainNetwork))
            {
                this.binanceSmartChainNetwork = BinanceSmartChainNetwork.Unknown;
            }

            if (this.binanceSmartChainNetwork == BinanceSmartChainNetwork.Main_Net || this.binanceSmartChainNetwork == BinanceSmartChainNetwork.Test_Net)
            {
                // For mainnet/testnet we always load defaults for hidden settings,
                // to avoid dealing with "stuck" values from old PG version that had different defaults.
                this.binanceSmartChainRPCURL = GetDefaultValue(BinanceSmartChainRPCTag);
            }
            else
            {
                this.binanceSmartChainRPCURL = PlayerPrefs.GetString(BinanceSmartChainRPCTag, GetDefaultValue(BinanceSmartChainRPCTag));
            }

            if (!BigInteger.TryParse(PlayerPrefs.GetString(BinanceSmartChainGasPriceGweiTag, "100"), out binanceSmartChainGasPriceGwei))
            {
                this.binanceSmartChainGasPriceGwei = 100;
            }
            if (!BigInteger.TryParse(PlayerPrefs.GetString(BinanceSmartChainTransferGasLimitTag, "21000"), out binanceSmartChainTransferGasLimit))
            {
                this.binanceSmartChainTransferGasLimit = 21000;
            }
            if (!BigInteger.TryParse(PlayerPrefs.GetString(BinanceSmartChainTokenTransferGasLimitTag, "100000"), out binanceSmartChainTokenTransferGasLimit))
            {
                this.binanceSmartChainTokenTransferGasLimit = 100000;
            }

            binanceSmartChainUserTokens = PlayerPrefs.GetString(BinanceSmartChainUserTokensTag, null);

            this.uiThemeName = PlayerPrefs.GetString(UiThemeNameTag, UiThemes.Phantasia.ToString());

            LoadLogSettings();

            this.ttrsNftSortMode = PlayerPrefs.GetInt(TtrsNftSortModeTag, 0);
            this.nftSortMode = PlayerPrefs.GetInt(NftSortModeTag, 0);
            this.nftSortDirection = PlayerPrefs.GetInt(NftSortDirectionTag, 0);

            var documentFolderPath = GetDocumentPath();
            this.lastVisitedFolder = PlayerPrefs.GetString(LastVisitedFolderTag, documentFolderPath);
            if (!System.IO.Directory.Exists(this.lastVisitedFolder))
                this.lastVisitedFolder = documentFolderPath;

            var mnemonicPhraseLength = PlayerPrefs.GetString(MnemonicPhraseLengthTag, MnemonicPhraseLength.Twelve_Words.ToString());
            if (!Enum.TryParse<MnemonicPhraseLength>(mnemonicPhraseLength, true, out this.mnemonicPhraseLength))
            {
                this.mnemonicPhraseLength = MnemonicPhraseLength.Twelve_Words;
            }

            var mnemonicPhraseVerificationMode = PlayerPrefs.GetString(MnemonicPhraseVerificationModeTag, MnemonicPhraseVerificationMode.Full.ToString());
            if (!Enum.TryParse<MnemonicPhraseVerificationMode>(mnemonicPhraseVerificationMode, true, out this.mnemonicPhraseVerificationMode))
            {
                this.mnemonicPhraseVerificationMode = MnemonicPhraseVerificationMode.Full;
            }

            var passwordMode = PlayerPrefs.GetString(PasswordModeTag, PasswordMode.Ask_Always.ToString());
            if (!Enum.TryParse<PasswordMode>(passwordMode, true, out this.passwordMode))
            {
                this.passwordMode = PasswordMode.Ask_Always;
            }

            Log.Write("Settings: Load: " + ToString());
        }

        public string GetDefaultValue(string tag)
        {
            string _return_value;

            switch (tag)
            {
                /*case PhantasmaRPCTag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            return "auto";

                        case NexusKind.Local_Net:
                            return "http://localhost:7077/rpc";

                        default:
                            return "http://45.76.88.140:7076/rpc";
                    }
                    break;
                    */

                case PhantasmaRPCTag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            _return_value = "http://207.148.17.86:7077/rpc";
                            break;

                        case NexusKind.Test_Net:
                            _return_value = "http://testnet.phantasma.io:7077/rpc";
                            break;

                        case NexusKind.Mankini_Test_Net:
                            _return_value = "http://mankinitest.phantasma.io:7077/rpc";
                            break;

                        case NexusKind.Local_Net:
                            _return_value = "http://localhost:7077/rpc";
                            break;

                        default:
                            _return_value = "http://207.148.17.86:7077/rpc";
                            break;
                    }
                    break;

                case PhantasmaExplorerTag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            _return_value = "https://explorer.phantasma.io";
                            break;

                        case NexusKind.Test_Net:
                            _return_value = "http://testnet.phantasma.io/";
                            break;

                        case NexusKind.Mankini_Test_Net:
                            _return_value = "http://mankinighost.phantasma.io:7083/";
                            break;

                        case NexusKind.Local_Net:
                            _return_value = "http://localhost:7074/";
                            break;

                        default:
                            _return_value = "https://explorer.phantasma.io";
                            break;
                    }
                    break;

                case PhantasmaNftExplorerTag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            _return_value = "https://ghostmarket.io/asset/pha";
                            break;

                        case NexusKind.Test_Net:
                            _return_value = "https://testnet.ghostmarket.io/asset/phat";
                            break;

                        case NexusKind.Mankini_Test_Net:
                            _return_value = "https://mankini.ghostmarket.io/asset/pha";
                            break;

                        case NexusKind.Local_Net:
                            _return_value = "https://dev.ghostmarket.io/asset/pha";
                            break;

                        default:
                            _return_value = "https://ghostmarket.io/asset/pha";
                            break;
                    }
                    break;

                case NeoRPCTag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            var neoRpcList = Poltergeist.Neo2.Utils.NeoRpcs.GetList();
                            int index = (int)(DateTime.UtcNow.Ticks % neoRpcList.Count);
                            _return_value = neoRpcList[index];
                            break;

                        case NexusKind.Test_Net:
                            _return_value = "http://mankinighost.phantasma.io:30333";
                            break;

                        case NexusKind.Mankini_Test_Net:
                            _return_value = "http://mankinighost.phantasma.io:30333";
                            break;

                        default:
                            _return_value = "http://mankinighost.phantasma.io:30333";
                            break;
                    }
                    break;

                case EthereumRPCTag:
                    switch (ethereumNetwork)
                    {
                        case EthereumNetwork.Main_Net:
                            _return_value = "https://mainnet.infura.io/v3/2bc1e4018304466d95d02f3f28d246b0";
                            break;

                        case EthereumNetwork.Ropsten:
                            _return_value = "https://ropsten.infura.io/v3/2bc1e4018304466d95d02f3f28d246b0";
                            break;

                        case EthereumNetwork.Local_Net:
                            _return_value = "http://mankinieth.phantasma.io:7545/";
                            break;

                        default:
                            _return_value = "";
                            break;
                    }
                    break;

                case BinanceSmartChainRPCTag:
                    switch (binanceSmartChainNetwork)
                    {
                        case BinanceSmartChainNetwork.Main_Net:
                            {
                                var bscRpcList = Phantasma.Bsc.Utils.BscRpcs.GetList(true);
                                int index = (int)(DateTime.UtcNow.Ticks % bscRpcList.Count);
                                _return_value = bscRpcList[index];
                                break;
                            }

                        case BinanceSmartChainNetwork.Test_Net:
                            {
                                var bscRpcList = Phantasma.Bsc.Utils.BscRpcs.GetList(false);
                                int index = (int)(DateTime.UtcNow.Ticks % bscRpcList.Count);
                                _return_value = bscRpcList[index];
                                break;
                            }

                        case BinanceSmartChainNetwork.Local_Net:
                            _return_value = "";
                            break;

                        default:
                            _return_value = "";
                            break;
                    }
                    break;

                case NeoscanAPITag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            _return_value = "https://neoscan.io";
                            break;

                        case NexusKind.Test_Net:
                            _return_value = "http://mankinighost.phantasma.io:4000";
                            break;

                        case NexusKind.Mankini_Test_Net:
                            _return_value = "http://mankinighost.phantasma.io:4000";
                            break;

                        default:
                            _return_value = "http://mankinighost.phantasma.io:4000";
                            break;
                    }
                    break;

                case NexusNameTag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            _return_value = "mainnet";
                            break;

                        case NexusKind.Test_Net:
                            _return_value = "testnet";
                            break;

                        case NexusKind.Mankini_Test_Net:
                            _return_value = "mainnet"; // TODO Currently this testnet works with mainnet nexus, should be changed later
                            break;

                        default:
                            _return_value = "simnet";
                            break;
                    }
                    break;

                default:
                    return "";
            }

            Log.Write("Settings: GetDefaultValue(" + tag + "->default): " + _return_value, Log.Level.Debug2);
            return _return_value;
        }

        public void Save()
        {
            PlayerPrefs.SetString(NexusKindTag, nexusKind.ToString());
            PlayerPrefs.SetString(PhantasmaRPCTag, this.phantasmaRPCURL);
            PlayerPrefs.SetString(PhantasmaExplorerTag, this.phantasmaExplorer);
            PlayerPrefs.SetString(PhantasmaNftExplorerTag, this.phantasmaNftExplorer);
            PlayerPrefs.SetString(GasPriceTag, this.feePrice.ToString());
            PlayerPrefs.SetString(GasLimitTag, this.feeLimit.ToString());
            PlayerPrefs.SetString(NeoGasFeeTag, this.neoGasFee.ToString());
            PlayerPrefs.SetString(NeoUserTokensTag, this.neoUserTokens);

            PlayerPrefs.SetString(NeoRPCTag, this.neoRPCURL);
            PlayerPrefs.SetString(NeoscanAPITag, this.neoscanURL);

            PlayerPrefs.SetString(EthereumNetworkTag, this.ethereumNetwork.ToString());
            PlayerPrefs.SetString(EthereumRPCTag, this.ethereumRPCURL);
            PlayerPrefs.SetString(EthereumGasPriceGweiTag, this.ethereumGasPriceGwei.ToString());
            PlayerPrefs.SetString(EthereumTransferGasLimitTag, this.ethereumTransferGasLimit.ToString());
            PlayerPrefs.SetString(EthereumTokenTransferGasLimitTag, this.ethereumTokenTransferGasLimit.ToString());
            PlayerPrefs.SetString(EthereumUserTokensTag, this.ethereumUserTokens);

            PlayerPrefs.SetString(BinanceSmartChainNetworkTag, this.binanceSmartChainNetwork.ToString());
            PlayerPrefs.SetString(BinanceSmartChainRPCTag, this.binanceSmartChainRPCURL);
            PlayerPrefs.SetString(BinanceSmartChainGasPriceGweiTag, this.binanceSmartChainGasPriceGwei.ToString());
            PlayerPrefs.SetString(BinanceSmartChainTransferGasLimitTag, this.binanceSmartChainTransferGasLimit.ToString());
            PlayerPrefs.SetString(BinanceSmartChainTokenTransferGasLimitTag, this.binanceSmartChainTokenTransferGasLimit.ToString());
            PlayerPrefs.SetString(BinanceSmartChainUserTokensTag, this.binanceSmartChainUserTokens);

            PlayerPrefs.SetString(NexusNameTag, this.nexusName);
            PlayerPrefs.SetString(CurrencyTag, this.currency);
            PlayerPrefs.SetInt(SFXTag, this.sfx ?1:0);
            PlayerPrefs.SetString(UiThemeNameTag, this.uiThemeName);
            PlayerPrefs.SetString(LogLevelTag, this.logLevel.ToString());
            PlayerPrefs.SetInt(LogOverwriteModeTag, this.logOverwriteMode ? 1 : 0);
            PlayerPrefs.SetString(MnemonicPhraseLengthTag, this.mnemonicPhraseLength.ToString());
            PlayerPrefs.SetString(MnemonicPhraseVerificationModeTag, this.mnemonicPhraseVerificationMode.ToString());
            PlayerPrefs.SetString(PasswordModeTag, this.passwordMode.ToString());
            PlayerPrefs.Save();

            Log.Write("Settings: Save: " + ToString());
        }

        public void SaveOnExit()
        {
            PlayerPrefs.SetInt(TtrsNftSortModeTag, this.ttrsNftSortMode);
            PlayerPrefs.SetInt(NftSortModeTag, this.nftSortMode);
            PlayerPrefs.SetInt(NftSortDirectionTag, this.nftSortDirection);
            PlayerPrefs.SetString(PhantasmaRPCTag, this.phantasmaRPCURL);
            PlayerPrefs.SetString(EthereumGasPriceGweiTag, this.ethereumGasPriceGwei.ToString());
            PlayerPrefs.SetString(BinanceSmartChainGasPriceGweiTag, this.binanceSmartChainGasPriceGwei.ToString());
            PlayerPrefs.SetString(LastVisitedFolderTag, this.lastVisitedFolder);
            PlayerPrefs.Save();

            Log.Write("Settings: Save on exit: TTRS NFT sort mode: " + ttrsNftSortMode + "\n" +
                      "                        NFT sort mode: " + nftSortMode + "\n" +
                      "                        NFT sort direction: " + nftSortDirection + "\n" +
                      "                        Phantasma RPC: " + phantasmaRPCURL + "\n" +
                      "                        Ethereum gas price (Gwei): " + ethereumGasPriceGwei + "\n" +
                      "                        BinanceSmartChain gas price (Gwei): " + binanceSmartChainGasPriceGwei,
                      Log.Level.Debug1);
        }

        public void RestoreEndpoints(bool restoreName)
        {
            //this.phantasmaRPCURL = this.GetDefaultValue(PhantasmaRPCTag);
            this.phantasmaRPCURL = this.GetDefaultValue(PhantasmaRPCTag);
            this.phantasmaExplorer = this.GetDefaultValue(PhantasmaExplorerTag);
            this.phantasmaNftExplorer = this.GetDefaultValue(PhantasmaNftExplorerTag);
            this.neoRPCURL = this.GetDefaultValue(NeoRPCTag);
            this.neoscanURL = this.GetDefaultValue(NeoscanAPITag);
            this.ethereumRPCURL = this.GetDefaultValue(EthereumRPCTag);
            this.binanceSmartChainRPCURL = this.GetDefaultValue(BinanceSmartChainRPCTag);

            if (restoreName)
            {
                this.nexusName = this.GetDefaultValue(NexusNameTag);

                // Reset Ethereum/BinanceSmartChain network on nexus change (except custom nexus).
                switch (this.nexusKind)
                {
                    case NexusKind.Main_Net:
                        this.ethereumNetwork = EthereumNetwork.Main_Net;
                        this.binanceSmartChainNetwork = BinanceSmartChainNetwork.Main_Net;
                        break;
                    case NexusKind.Test_Net:
                        this.ethereumNetwork = EthereumNetwork.Ropsten;
                        this.binanceSmartChainNetwork = BinanceSmartChainNetwork.Test_Net;
                        break;
                    case NexusKind.Mankini_Test_Net:
                        this.ethereumNetwork = EthereumNetwork.Ropsten;
                        this.binanceSmartChainNetwork = BinanceSmartChainNetwork.Test_Net;
                        break;
                    case NexusKind.Local_Net:
                        this.ethereumNetwork = EthereumNetwork.Ropsten;
                        this.binanceSmartChainNetwork = BinanceSmartChainNetwork.Test_Net;
                        break;
                }
            }

            Log.Write("Settings: Restore endpoints: restoreName mode: " + restoreName + "\n" +
                      "                             Phantasma RPC: " + this.phantasmaRPCURL + "\n" +
                      "                             Phantasma Explorer: " + this.phantasmaExplorer + "\n" +
                      "                             Phantasma NFT Explorer: " + this.phantasmaNftExplorer + "\n" +
                      "                             Neo RPC: " + this.neoRPCURL + "\n" +
                      "                             Neoscan: " + this.neoscanURL + "\n" +
                      "                             Ethereum RPC: " + this.ethereumRPCURL + "\n" +
                      "                             BinanceSmartChain RPC: " + this.binanceSmartChainRPCURL + "\n" +
                      "                             Nexus name: " + this.nexusName,
                      Log.Level.Debug1);
        }

        public void RestoreEthereumEndpoint()
        {
            this.ethereumRPCURL = this.GetDefaultValue(EthereumRPCTag);

            Log.Write("Settings: Restore ethereum endpoint:\n" +
                      "                             Ethereum RPC: " + this.ethereumRPCURL,
                      Log.Level.Debug1);
        }

        public void RestoreBinanceSmartChainEndpoint()
        {
            this.binanceSmartChainRPCURL = this.GetDefaultValue(BinanceSmartChainRPCTag);

            Log.Write("Settings: Restore BinanceSmartChain endpoint:\n" +
                      "                             BinanceSmartChain RPC: " + this.binanceSmartChainRPCURL,
                      Log.Level.Debug1);
        }

        private string GetDocumentPath()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
                return System.IO.Path.Combine(Environment.ExpandEnvironmentVariables("%userprofile%"), "Documents");
            else if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
                return System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/Documents/";
            else if (Application.platform == RuntimePlatform.LinuxPlayer)
                return System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            else
            {
                return Application.persistentDataPath;
            }
        }
        public string GetLastVisitedFolder()
        {
            return this.lastVisitedFolder;
        }
        public void SetLastVisitedFolder(string folderPath)
        {
            this.lastVisitedFolder = folderPath;
        }
    }
}
