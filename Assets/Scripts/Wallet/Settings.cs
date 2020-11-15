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

    public enum UiThemes
    {
        Classic,
        Phantasia
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
        //public const string PhantasmaRPCTag = "settings.phantasma.rpc.url";
        public const string PhantasmaBPTag = "settings.phantasma.bp.url";
        public const string PhantasmaExplorerTag = "settings.phantasma.explorer.url";
        public const string NeoRPCTag = "settings.neo.rpc.url";
        public const string NeoscanAPITag = "settings.neoscan.url";
        public const string NexusNameTag = "settings.nexus.name";

        public const string NexusKindTag = "settings.nexus.kind";
        public const string CurrencyTag = "settings.currency";
        public const string GasPriceTag = "settings.fee.price";

        public const string NeoGasFeeTag = "settings.neo.gas.fee";

        public const string EthereumNetworkTag = "settings.ethereum.network";
        public const string EthereumLocalnetSoulContractTag = "settings.ethereum.localnet.soul.contract";
        public const string EthereumLocalnetKcalContractTag = "settings.ethereum.localnet.kcal.contract";
        public const string EthereumRPCTag = "settings.ethereum.rpc.url";
        public const string EthereumGasPriceGweiTag = "settings.ethereum.gas.price.gwei";
        public const string EthereumTransferGasLimitTag = "settings.ethereum.transfer.gas.limit";
        public const string EthereumTokenTransferGasLimitTag = "settings.ethereum.token.transfer.gas.limit";

        public const string SFXTag = "settings.sfx";

        public const string LogLevelTag = "log.level";
        public const string LogOverwriteModeTag = "log.overwrite.mode";

        public const string UiThemeNameTag = "ui.theme.name";

        public const string TtrsNftSortModeTag = "ttrs.nft.sort.mode";
        public const string NftSortDirectionTag = "nft.sort.direction";

        public const string LastVisitedFolderTag = "last.visited.folder";

        public string phantasmaRPCURL;
        public string phantasmaBPURL;
        public string phantasmaExplorer;
        public string neoRPCURL;
        public string neoscanURL;
        public string nexusName;
        public string currency;
        public BigInteger feePrice;
        public decimal neoGasFee;
        public EthereumNetwork ethereumNetwork;
        public string ethereumLocalnetSoulContract;
        public string ethereumLocalnetKcalContract;
        public string ethereumRPCURL;
        public BigInteger ethereumGasPriceGwei;
        public BigInteger ethereumTransferGasLimit;
        public BigInteger ethereumTokenTransferGasLimit;
        public NexusKind nexusKind;
        public bool sfx;
        public Log.Level logLevel;
        public bool logOverwriteMode;
        public string uiThemeName;
        public int ttrsNftSortMode;
        public int nftSortDirection;
        public string lastVisitedFolder;

        public override string ToString()
        {
            return "Nexus kind: " + this.nexusKind.ToString() + "\n" +
                "Phantasma BP: " + this.phantasmaBPURL + "\n" +
                "Phantasma RPC: " + this.phantasmaRPCURL + "\n" +
                "Fee price: " + this.feePrice + "\n" +
                "Neo RPC: " + this.neoRPCURL + "\n" +
                "Neoscan: " + this.neoscanURL + "\n" +
                "Neo GAS fee: " + this.neoGasFee + "\n" +
                "Ethereum network: " + this.ethereumNetwork + "\n" +
                "Ethereum localnet SOUL contract: " + this.ethereumLocalnetSoulContract + "\n" +
                "Ethereum localnet KCAL contract: " + this.ethereumLocalnetKcalContract + "\n" +
                "Ethereum RPC: " + this.ethereumRPCURL + "\n" +
                "Ethereum gas price (Gwei): " + this.ethereumGasPriceGwei + "\n" +
                "Ethereum transfer gas limit: " + this.ethereumTransferGasLimit + "\n" +
                "Ethereum token transfer gas limit: " + this.ethereumTokenTransferGasLimit + "\n" +
                "Nexus name: " + this.nexusName + "\n" +
                "Currency: " + this.currency + "\n" +
                "Sfx: " + this.sfx + "\n" +
                "UI theme: " + this.uiThemeName + "\n" +
                "Log level: " + this.logLevel + "\n" +
                "Log overwrite: " + this.logOverwriteMode + "\n" +
                "TTRS NFT sort mode: " + this.ttrsNftSortMode + "\n" +
                "NFT sort direction: " + this.nftSortDirection;
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

            if (this.nexusKind == NexusKind.Main_Net || this.nexusKind == NexusKind.Test_Net)
            {
                // For mainnet/testnet we always load defaults for hidden settings,
                // to avoid dealing with "stuck" values from old PG version that had different defaults.
                this.phantasmaBPURL = GetDefaultValue(PhantasmaBPTag);
                this.phantasmaExplorer = GetDefaultValue(PhantasmaExplorerTag);
                this.neoRPCURL = GetDefaultValue(NeoRPCTag);
                this.neoscanURL = GetDefaultValue(NeoscanAPITag);
                this.nexusName = GetDefaultValue(NexusNameTag);
            }
            else
            {
                //this.phantasmaRPCURL = PlayerPrefs.GetString(PhantasmaRPCTag, GetDefaultValue(PhantasmaRPCTag));
                this.phantasmaBPURL = PlayerPrefs.GetString(PhantasmaBPTag, GetDefaultValue(PhantasmaBPTag));
                this.phantasmaExplorer = PlayerPrefs.GetString(PhantasmaExplorerTag, GetDefaultValue(PhantasmaExplorerTag));
                this.neoRPCURL = PlayerPrefs.GetString(NeoRPCTag, GetDefaultValue(NeoRPCTag));
                this.neoscanURL = PlayerPrefs.GetString(NeoscanAPITag, GetDefaultValue(NeoscanAPITag));
                this.nexusName = PlayerPrefs.GetString(NexusNameTag, GetDefaultValue(NexusNameTag));
            }

            this.currency = PlayerPrefs.GetString(CurrencyTag, "USD");
            this.sfx = PlayerPrefs.GetInt(SFXTag, 0)!=0;

            this.phantasmaRPCURL = this.phantasmaBPURL;

            var defaultGasPrice = 100000;
            if (!BigInteger.TryParse(PlayerPrefs.GetString(GasPriceTag, defaultGasPrice.ToString()), out feePrice))
            {
                this.feePrice = 100000;
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

            // Ethereum
            var ethereumNetwork = PlayerPrefs.GetString(EthereumNetworkTag, EthereumNetwork.Main_Net.ToString());
            if (!Enum.TryParse<EthereumNetwork>(ethereumNetwork, true, out this.ethereumNetwork))
            {
                this.ethereumNetwork = EthereumNetwork.Unknown;
            }

            this.ethereumLocalnetSoulContract = PlayerPrefs.GetString(EthereumLocalnetSoulContractTag, GetDefaultValue(EthereumLocalnetSoulContractTag));
            this.ethereumLocalnetKcalContract = PlayerPrefs.GetString(EthereumLocalnetKcalContractTag, GetDefaultValue(EthereumLocalnetKcalContractTag));

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

            this.uiThemeName = PlayerPrefs.GetString(UiThemeNameTag, UiThemes.Phantasia.ToString());

            LoadLogSettings();

            this.ttrsNftSortMode = PlayerPrefs.GetInt(TtrsNftSortModeTag, 0);
            this.nftSortDirection = PlayerPrefs.GetInt(NftSortDirectionTag, 0);

            var documentFolderPath = GetDocumentPath();
            this.lastVisitedFolder = PlayerPrefs.GetString(LastVisitedFolderTag, documentFolderPath);
            if (!System.IO.Directory.Exists(this.lastVisitedFolder))
                this.lastVisitedFolder = documentFolderPath;

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

                case PhantasmaBPTag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            _return_value = "http://207.148.17.86:7077/rpc";
                            break;

                        case NexusKind.Test_Net:
                            _return_value = "http://testnet.phantasma.io:7077/rpc";
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

                        case NexusKind.Local_Net:
                            _return_value = "http://localhost:7074/";
                            break;

                        default:
                            _return_value = "https://explorer.phantasma.io";
                            break;
                    }
                    break;

                case NeoRPCTag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            var neoRpcList = Phantasma.Neo.Utils.NeoRpcs.GetList();
                            int index = (int)(DateTime.UtcNow.Ticks % neoRpcList.Count);
                            _return_value = neoRpcList[index];
                            break;

                        case NexusKind.Test_Net:
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

                case EthereumLocalnetSoulContractTag:
                    _return_value = "4c2AF2fB374B988363deb535Bf0fF2D1Eb7b2106"; // Value from http://mankinieth.phantasma.io:7545/
                    break;

                case EthereumLocalnetKcalContractTag:
                    _return_value = "a9858F0E2037C18dD6a0b4Bc082d41B0536D47E2"; // Value from http://mankinieth.phantasma.io:7545/
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
            //PlayerPrefs.SetString(PhantasmaRPCTag, this.phantasmaRPCURL);
            PlayerPrefs.SetString(PhantasmaBPTag, this.phantasmaBPURL);
            PlayerPrefs.SetString(PhantasmaExplorerTag, this.phantasmaExplorer);
            PlayerPrefs.SetString(GasPriceTag, this.feePrice.ToString());
            PlayerPrefs.SetString(NeoGasFeeTag, this.neoGasFee.ToString());

            PlayerPrefs.SetString(NeoRPCTag, this.neoRPCURL);
            PlayerPrefs.SetString(NeoscanAPITag, this.neoscanURL);

            PlayerPrefs.SetString(EthereumNetworkTag, this.ethereumNetwork.ToString());
            PlayerPrefs.SetString(EthereumLocalnetSoulContractTag, this.ethereumLocalnetSoulContract.ToString());
            PlayerPrefs.SetString(EthereumLocalnetKcalContractTag, this.ethereumLocalnetKcalContract.ToString());
            PlayerPrefs.SetString(EthereumRPCTag, this.ethereumRPCURL);
            PlayerPrefs.SetString(EthereumGasPriceGweiTag, this.ethereumGasPriceGwei.ToString());
            PlayerPrefs.SetString(EthereumTransferGasLimitTag, this.ethereumTransferGasLimit.ToString());
            PlayerPrefs.SetString(EthereumTokenTransferGasLimitTag, this.ethereumTokenTransferGasLimit.ToString());

            PlayerPrefs.SetString(NexusNameTag, this.nexusName);
            PlayerPrefs.SetString(CurrencyTag, this.currency);
            PlayerPrefs.SetInt(SFXTag, this.sfx ?1:0);
            PlayerPrefs.SetString(UiThemeNameTag, this.uiThemeName);
            PlayerPrefs.SetString(LogLevelTag, this.logLevel.ToString());
            PlayerPrefs.SetInt(LogOverwriteModeTag, this.logOverwriteMode ? 1 : 0);
            PlayerPrefs.Save();

            Log.Write("Settings: Save: " + ToString());
        }

        public void SaveOnExit()
        {
            PlayerPrefs.SetInt(TtrsNftSortModeTag, this.ttrsNftSortMode);
            PlayerPrefs.SetInt(NftSortDirectionTag, this.nftSortDirection);
            PlayerPrefs.SetString(PhantasmaBPTag, this.phantasmaBPURL);
            PlayerPrefs.SetString(EthereumGasPriceGweiTag, this.ethereumGasPriceGwei.ToString());
            PlayerPrefs.SetString(LastVisitedFolderTag, this.lastVisitedFolder);
            PlayerPrefs.Save();

            Log.Write("Settings: Save on exit: TTRS NFT sort mode: " + ttrsNftSortMode + "\n" +
                      "                        NFT sort direction: " + nftSortDirection + "\n" +
                      "                        Phantasma BP: " + phantasmaBPURL + "\n" +
                      "                        Ethereum gas price (Gwei): " + ethereumGasPriceGwei,
                      Log.Level.Debug1);
        }

        public void RestoreEndpoints(bool restoreName)
        {
            //this.phantasmaRPCURL = this.GetDefaultValue(PhantasmaRPCTag);
            this.phantasmaBPURL = this.GetDefaultValue(PhantasmaBPTag);
            this.phantasmaExplorer = this.GetDefaultValue(PhantasmaExplorerTag);
            this.neoRPCURL = this.GetDefaultValue(NeoRPCTag);
            this.neoscanURL = this.GetDefaultValue(NeoscanAPITag);
            this.ethereumRPCURL = this.GetDefaultValue(EthereumRPCTag);

            if (restoreName)
            {
                this.nexusName = this.GetDefaultValue(NexusNameTag);

                // Reset ethereum network on nexus change (except custom nexus).
                switch (this.nexusKind)
                {
                    case NexusKind.Main_Net:
                        this.ethereumNetwork = EthereumNetwork.Main_Net;
                        break;
                    case NexusKind.Test_Net:
                        this.ethereumNetwork = EthereumNetwork.Ropsten;
                        break;
                    case NexusKind.Local_Net:
                        this.ethereumNetwork = EthereumNetwork.Ropsten;
                        break;
                }
            }

            Log.Write("Settings: Restore endpoints: restoreName mode: " + restoreName + "\n" +
                      "                             Phantasma BP: " + this.phantasmaBPURL + "\n" +
                      "                             Phantasma Explorer: " + this.phantasmaExplorer + "\n" +
                      "                             Neo RPC: " + this.neoRPCURL + "\n" +
                      "                             Neoscan: " + this.neoscanURL + "\n" +
                      "                             Ethereum RPC: " + this.ethereumRPCURL + "\n" +
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
