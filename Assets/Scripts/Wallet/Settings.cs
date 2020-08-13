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
        public const string NeoRPCTag = "settings.neo.rpc.url";
        public const string NeoscanAPITag = "settings.neoscan.url";
        public const string NexusNameTag = "settings.nexus.name";

        public const string NexusKindTag = "settings.nexus.kind";
        public const string CurrencyTag = "settings.currency";
        public const string GasPriceTag = "settings.fee.price";

        public const string NeoGasFeeTag = "settings.neo.gas.fee";

        public const string EthereumRPCTag = "settings.ethereum.rpc.url";
        public const string EthereumGasPriceGweiTag = "settings.ethereum.gas.price.gwei";
        public const string EthereumTransferGasLimitTag = "settings.ethereum.transfer.gas.limit";
        public const string EthereumTokenTransferGasLimitTag = "settings.ethereum.token.transfer.gas.limit";
        public const string EthereumContractGasLimitTag = "settings.ethereum.contract.gas.limit";

        public const string SFXTag = "settings.sfx";

        public const string LogLevelTag = "log.level";
        public const string LogOverwriteModeTag = "log.overwrite.mode";

        public const string UiThemeNameTag = "ui.theme.name";

        public const string TtrsNftSortModeTag = "ttrs.nft.sort.mode";
        public const string NftSortDirectionTag = "nft.sort.direction";

        public string phantasmaRPCURL;
        public string phantasmaBPURL;
        public string neoRPCURL;
        public string neoscanURL;
        public string nexusName;
        public string currency;
        public BigInteger feePrice;
        public decimal neoGasFee;
        public string ethereumRPCURL;
        public BigInteger ethereumGasPriceGwei;
        public BigInteger ethereumTransferGasLimit;
        public BigInteger ethereumTokenTransferGasLimit;
        public BigInteger ethereumContractGasLimit;
        public NexusKind nexusKind;
        public bool sfx;
        public Log.Level logLevel;
        public bool logOverwriteMode;
        public string uiThemeName;
        public int ttrsNftSortMode;
        public int nftSortDirection;

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

            //this.phantasmaRPCURL = PlayerPrefs.GetString(PhantasmaRPCTag, GetDefaultValue(PhantasmaRPCTag));
            this.phantasmaBPURL = PlayerPrefs.GetString(PhantasmaBPTag, GetDefaultValue(PhantasmaBPTag));
            this.neoRPCURL = PlayerPrefs.GetString(NeoRPCTag, GetDefaultValue(NeoRPCTag));
            this.neoscanURL = PlayerPrefs.GetString(NeoscanAPITag, GetDefaultValue(NeoscanAPITag));
            this.nexusName = PlayerPrefs.GetString(NexusNameTag, GetDefaultValue(NexusNameTag));

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
            this.ethereumRPCURL = PlayerPrefs.GetString(EthereumRPCTag, GetDefaultValue(EthereumRPCTag));
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
            if (!BigInteger.TryParse(PlayerPrefs.GetString(EthereumContractGasLimitTag, "200000"), out ethereumContractGasLimit))
            {
                this.ethereumContractGasLimit = 200000;
            }

            this.uiThemeName = PlayerPrefs.GetString(UiThemeNameTag, UiThemes.Phantasia.ToString());

            LoadLogSettings();

            this.ttrsNftSortMode = PlayerPrefs.GetInt(TtrsNftSortModeTag, 0);
            this.nftSortDirection = PlayerPrefs.GetInt(NftSortDirectionTag, 0);

            Log.Write("Settings: Load: Nexus kind: " + this.nexusKind.ToString() + "\n" +
                      "                Phantasma BP: " + this.phantasmaBPURL + "\n" +
                      "                Phantasma RPC: " + this.phantasmaRPCURL + "\n" +
                      "                Fee price: " + this.feePrice + "\n" +
                      "                Neo RPC: " + this.neoRPCURL + "\n" +
                      "                Neoscan: " + this.neoscanURL + "\n" +
                      "                Neo GAS fee: " + this.neoGasFee + "\n" +
                      "                Ethereum RPC: " + this.ethereumRPCURL + "\n" +
                      "                Ethereum gas price (Gwei): " + this.ethereumGasPriceGwei + "\n" +
                      "                Ethereum transfer gas limit: " + this.ethereumTransferGasLimit + "\n" +
                      "                Ethereum token transfer gas limit: " + this.ethereumTokenTransferGasLimit + "\n" +
                      "                Ethereum contract gas limit: " + this.ethereumContractGasLimit + "\n" +
                      "                Nexus name: " + this.nexusName + "\n" +
                      "                Currency: " + this.currency + "\n" +
                      "                Sfx: " + this.sfx + "\n" +
                      "                UI theme: " + this.uiThemeName + "\n" +
                      "                Log level: " + this.logLevel + "\n" +
                      "                Log overwrite: " + this.logOverwriteMode + "\n" +
                      "                TTRS NFT sort mode: " + this.ttrsNftSortMode + "\n" +
                      "                NFT sort direction: " + this.nftSortDirection
                     );
        }

        public string GetDefaultValue(string tag)
        {
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
                            {
                                string _return_value = "http://207.148.17.86:7077/rpc";
                                Log.Write("Settings: GetDefaultValue(" + tag + "->NexusKind.Main_Net): " + _return_value, Log.Level.Debug2);
                                return _return_value;
                            }

                        case NexusKind.Local_Net:
                            {
                                string _return_value = "http://localhost:7077/rpc";
                                Log.Write("Settings: GetDefaultValue(" + tag + "->NexusKind.Local_Net): " + _return_value, Log.Level.Debug2);
                                return _return_value;
                            }

                        default:
                            {
                                string _return_value = "http://207.148.17.86:7077/rpc";
                                Log.Write("Settings: GetDefaultValue(" + tag + "->default): " + _return_value, Log.Level.Debug2);
                                return _return_value;
                            }
                    }
                    break;

                case NeoRPCTag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            {
                                var neoRpcList = Phantasma.Neo.Utils.NeoRpcs.GetList();
                                int index = (int)(DateTime.UtcNow.Ticks % neoRpcList.Count);
                                string _return_value = neoRpcList[index];
                                Log.Write("Settings: GetDefaultValue(" + tag + "->NexusKind.Main_Net): " + _return_value, Log.Level.Debug2);
                                return _return_value;
                            }

                        default:
                            {
                                string _return_value = "http://mankinighost.phantasma.io:30333";
                                Log.Write("Settings: GetDefaultValue(" + tag + "->default): " + _return_value, Log.Level.Debug2);
                                return _return_value;
                            }
                    }
                    break;

                case EthereumRPCTag:
                    switch (nexusKind)
                    {
                        /*case NexusKind.Main_Net:
                            {
                                string _return_value = "";
                                Log.Write("Settings: GetDefaultValue(" + tag + "->default): " + _return_value, Log.Level.Debug2);
                                return _return_value;
                            }*/

                        default:
                            {
                                string _return_value = "http://13.91.228.58:7545";
                                Log.Write("Settings: GetDefaultValue(" + tag + "->default): " + _return_value, Log.Level.Debug2);
                                return _return_value;
                            }
                    }
                    break;

                case NeoscanAPITag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            {
                                string _return_value = "https://neoscan.io";
                                Log.Write("Settings: GetDefaultValue(" + tag + "->default): " + _return_value, Log.Level.Debug2);
                                return _return_value;
                            }

                        default:
                            {
                                string _return_value = "http://mankinighost.phantasma.io:4000";
                                Log.Write("Settings: GetDefaultValue(" + tag + "->default): " + _return_value, Log.Level.Debug2);
                                return _return_value;
                            }
                    }
                    break;

                case NexusNameTag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            {
                                string _return_value = "mainnet";
                                Log.Write("Settings: GetDefaultValue(" + tag + "->default): " + _return_value, Log.Level.Debug2);
                                return _return_value;
                            }

                        default:
                            {
                                string _return_value = "simnet";
                                Log.Write("Settings: GetDefaultValue(" + tag + "->default): " + _return_value, Log.Level.Debug2);
                                return _return_value;
                            }
                    }
                    break;

                default:
                    return "";
            }
        }

        public void Save()
        {
            PlayerPrefs.SetString(NexusKindTag, nexusKind.ToString());
            //PlayerPrefs.SetString(PhantasmaRPCTag, this.phantasmaRPCURL);
            PlayerPrefs.SetString(PhantasmaBPTag, this.phantasmaBPURL);
            PlayerPrefs.SetString(GasPriceTag, this.feePrice.ToString());
            PlayerPrefs.SetString(NeoGasFeeTag, this.neoGasFee.ToString());

            PlayerPrefs.SetString(NeoRPCTag, this.neoRPCURL);
            PlayerPrefs.SetString(NeoscanAPITag, this.neoscanURL);

            PlayerPrefs.SetString(EthereumRPCTag, this.ethereumRPCURL);
            PlayerPrefs.SetString(EthereumGasPriceGweiTag, this.ethereumGasPriceGwei.ToString());
            PlayerPrefs.SetString(EthereumTransferGasLimitTag, this.ethereumTransferGasLimit.ToString());
            PlayerPrefs.SetString(EthereumTokenTransferGasLimitTag, this.ethereumTokenTransferGasLimit.ToString());
            PlayerPrefs.SetString(EthereumContractGasLimitTag, this.ethereumContractGasLimit.ToString());

            PlayerPrefs.SetString(NexusNameTag, this.nexusName);
            PlayerPrefs.SetString(CurrencyTag, this.currency);
            PlayerPrefs.SetInt(SFXTag, this.sfx ?1:0);
            PlayerPrefs.SetString(UiThemeNameTag, this.uiThemeName);
            PlayerPrefs.SetString(LogLevelTag, this.logLevel.ToString());
            PlayerPrefs.SetInt(LogOverwriteModeTag, this.logOverwriteMode ? 1 : 0);
            PlayerPrefs.Save();

            Log.Write("Settings: Save: Nexus kind: " + nexusKind.ToString() + "\n" +
                      "                Phantasma BP: " + phantasmaBPURL + "\n" +
                      "                Fee price: " + feePrice + "\n" +
                      "                Neo RPC: " + neoRPCURL + "\n" +
                      "                Neoscan: " + neoscanURL + "\n" +
                      "                Neo GAS fee: " + neoGasFee + "\n" +
                      "                Ethereum RPC: " + ethereumRPCURL + "\n" +
                      "                Ethereum gas price (Gwei): " + EthereumGasPriceGweiTag + "\n" +
                      "                Ethereum transfer gas limit: " + this.ethereumTransferGasLimit + "\n" +
                      "                Ethereum token transfer gas limit: " + this.ethereumTokenTransferGasLimit + "\n" +
                      "                Ethereum contract gas limit: " + this.ethereumContractGasLimit + "\n" +
                      "                Nexus name: " + nexusName + "\n" +
                      "                Currency: " + currency + "\n" +
                      "                Sfx: " + sfx + "\n" +
                      "                UI Theme: " + uiThemeName + "\n" +
                      "                Log level: " + logLevel.ToString() + "\n" +
                      "                Log overwrite: " + logOverwriteMode
                     );
        }

        public void SaveOnExit()
        {
            PlayerPrefs.SetInt(TtrsNftSortModeTag, this.ttrsNftSortMode);
            PlayerPrefs.SetInt(NftSortDirectionTag, this.nftSortDirection);
            PlayerPrefs.SetString(PhantasmaBPTag, this.phantasmaBPURL);
            PlayerPrefs.Save();

            Log.Write("Settings: Save on exit: TTRS NFT sort mode: " + ttrsNftSortMode + "\n" +
                      "                        NFT sort direction: " + nftSortDirection + "\n" +
                      "                        Phantasma BP: " + phantasmaBPURL,
                      Log.Level.Debug1);
        }

        public void RestoreEndpoints(bool restoreName)
        {
            //this.phantasmaRPCURL = this.GetDefaultValue(PhantasmaRPCTag);
            this.phantasmaBPURL = this.GetDefaultValue(PhantasmaBPTag);
            this.neoRPCURL = this.GetDefaultValue(NeoRPCTag);
            this.neoscanURL = this.GetDefaultValue(NeoscanAPITag);
            this.ethereumRPCURL = this.GetDefaultValue(EthereumRPCTag);

            if (restoreName)
            {
                this.nexusName = this.GetDefaultValue(NexusNameTag);
            }

            Log.Write("Settings: Restore endpoints: restoreName mode: " + restoreName + "\n" +
                      "                             Phantasma BP: " + this.phantasmaBPURL + "\n" +
                      "                             Neo RPC: " + this.neoRPCURL + "\n" +
                      "                             Neoscan: " + this.neoscanURL + "\n" +
                      "                             Ethereum RPC: " + this.ethereumRPCURL + "\n" +
                      "                             Nexus name: " + this.nexusName,
                      Log.Level.Debug1);
        }
    }
}
