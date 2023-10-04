namespace Poltergeist
{
    public struct TransferRequest
    {
        public PlatformKind platform;
        public string destination;
        public string symbol;
        public decimal amount;
        public string interop;
    }
}