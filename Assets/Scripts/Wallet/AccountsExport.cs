namespace Poltergeist
{
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
}