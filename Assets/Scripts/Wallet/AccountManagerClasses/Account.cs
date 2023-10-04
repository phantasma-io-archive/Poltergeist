using System;

namespace Poltergeist
{
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
}
