using UnityEngine;

namespace Poltergeist
{
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
        private const string PhantasmaRPCTag = "settings.phantasma.rpc.url";
        private const string NeoRPCTag = "settings.neo.rpc.url";
        private const string NeoscanAPITag = "settings.neoscan.api";
        private const string NexusNameTag = "settings.nexus.name";
        private const string CurrencyTag = "settings.currency";

        public string phantasmaRPCURL;
        public string neoRPCURL;
        public string neoscanAPIURL;
        public string nexusName;
        public string currency;

        public void Load()
        {
#if UNITY_EDITOR
            string defaultRPC = "http://localhost:7077/rpc";
#else
            string defaultRPC = "http://45.76.88.140:7076/rpc";
#endif

            this.phantasmaRPCURL = PlayerPrefs.GetString(PhantasmaRPCTag, defaultRPC);
            this.neoRPCURL = PlayerPrefs.GetString(NeoRPCTag, "http://mankinighost.phantasma.io:30333");
            this.neoscanAPIURL = PlayerPrefs.GetString(NeoscanAPITag, "http://mankinighost.phantasma.io:4000");

            this.nexusName = PlayerPrefs.GetString(NexusNameTag, "simnet");
            this.currency = PlayerPrefs.GetString(CurrencyTag, "USD");
        }

        public void Save()
        {

        }
    }
}
