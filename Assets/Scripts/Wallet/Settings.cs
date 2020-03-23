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
        public const string SFXTag = "settings.sfx";

        public string phantasmaRPCURL;
        public string phantasmaBPURL;
        public string neoRPCURL;
        public string neoscanURL;
        public string nexusName;
        public string currency;
        public BigInteger feePrice;
        public NexusKind nexusKind;
        public bool sfx;

        public void Load()
        {
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
            this.sfx = PlayerPrefs.GetInt(SFXTag, 1)!=0;

            this.phantasmaRPCURL = this.phantasmaBPURL;

            var defaultGasPrice = 100000;
            if (!BigInteger.TryParse(PlayerPrefs.GetString(GasPriceTag, defaultGasPrice.ToString()), out feePrice))
            {
                this.feePrice = 100000;
            }
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
                            return "http://207.148.17.86:7077/rpc";

                        case NexusKind.Local_Net:
                            return "http://localhost:7077/rpc";

                        default:
                            return "http://45.76.88.140:7076/rpc";
                    }
                    break;

                case NeoRPCTag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            int index = (int)(DateTime.UtcNow.Ticks % 8)+1;
                            return $"https://seed{index}.cityofzion.io:443";

                        default:
                            return "http://mankinighost.phantasma.io:30333";
                    }
                    break;
                    
                case NeoscanAPITag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            return "https://neoscan.io";

                        default:
                            return "http://mankinighost.phantasma.io:4000";
                    }
                    break;

                case NexusNameTag:
                    switch (nexusKind)
                    {
                        case NexusKind.Main_Net:
                            return "mainnet";

                        default:
                            return "simnet";
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
            PlayerPrefs.SetString(NeoRPCTag, this.neoRPCURL);
            PlayerPrefs.SetString(NeoscanAPITag, this.neoscanURL);
            PlayerPrefs.SetString(NexusNameTag, this.nexusName);
            PlayerPrefs.SetString(CurrencyTag, this.currency);
            PlayerPrefs.SetString(GasPriceTag, this.feePrice.ToString());
            PlayerPrefs.SetInt(SFXTag, this.sfx ?1:0);
            PlayerPrefs.Save();
        }

        public void RestoreEndpoints(bool restoreName)
        {
            //this.phantasmaRPCURL = this.GetDefaultValue(PhantasmaRPCTag);
            this.phantasmaBPURL = this.GetDefaultValue(PhantasmaBPTag);
            this.neoRPCURL = this.GetDefaultValue(NeoRPCTag);
            this.neoscanURL = this.GetDefaultValue(NeoscanAPITag);

            if (restoreName)
            {
                this.nexusName = this.GetDefaultValue(NexusNameTag);
            }
        }
    }
}
