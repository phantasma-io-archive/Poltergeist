namespace Poltergeist
{
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
}
