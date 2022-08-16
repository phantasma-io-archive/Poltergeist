using Poltergeist.PhantasmaLegacy.Core.Types;
using Poltergeist.PhantasmaLegacy.Cryptography;

namespace Poltergeist.PhantasmaLegacy.Domain
{
    public enum TriggerResult
    {
        Failure,
        Missing,
        Success,
    }

    public enum TokenTrigger
    {
        OnMint, // address, symbol, amount
        OnBurn, // address, symbol, amount
        OnSend, // address, symbol, amount
        OnReceive, // address, symbol, amount
        OnInfuse, // address, symbol, amount
        OnUpgrade, // address
        OnSeries, // address
    }

    public struct StakeReward
    {
        public readonly Address staker;
        public readonly Timestamp date;

        public StakeReward(Address staker, Timestamp date)
        {
            this.staker = staker;
            this.date = date;
        }
    }       

    public static class DomainSettings
    {
        public const int LatestKnownProtocol = 5;

        public const int MAX_TOKEN_DECIMALS = 18;

        public const string FuelTokenSymbol = "KCAL";
        public const string FuelTokenName = "Phantasma Energy";
        public const int FuelTokenDecimals = 10;

        public const string NexusMainnet = "mainnet";
        public const string NexusTestnet = "testnet";

        public const string StakingTokenSymbol = "SOUL";
        public const string StakingTokenName = "Phantasma Stake";
        public const int StakingTokenDecimals = 8;

        public const string FiatTokenSymbol = "USD";
        public const string FiatTokenName = "Dollars";
        public const int FiatTokenDecimals = 8;

        public const string RewardTokenSymbol = "CROWN";
        public const string RewardTokenName = "Phantasma Crown";

        public const string RootChainName = "main";

        public const string ValidatorsOrganizationName = "validators";
        public const string MastersOrganizationName = "masters";
        public const string StakersOrganizationName = "stakers";

        public const string PhantomForceOrganizationName = "phantom_force";

        public static readonly int ArchiveMinSize = 64; // in bytes
        public static readonly int ArchiveMaxSize = 104857600; //100mb
        public static readonly uint ArchiveBlockSize = MerkleTree.ChunkSize;
    }
}
