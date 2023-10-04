namespace Poltergeist
{
    public enum MessageKind
    {
        Default,
        Error,
        Success
    }

    public struct MenuEntry
    {
        public readonly object value;
        public readonly string label;
        public readonly bool enabled;

        public MenuEntry(object value, string label, bool enabled)
        {
            this.value = value;
            this.label = label;
            this.enabled = enabled;
        }
    }

    public enum GUIState
    {
        Loading,
        Wallets,
        Balances,
        Nft, // Full list of NFTs with sorting and filtering capabilities.
        NftView, // Full list of NFTs with sorting and filtering capabilities, view only mode.
        NftTransferList, // List of user-selected NFTs, ready to be transfered to another wallet.
        History,
        Account,
        Sending,
        Confirming,
        WalletsManagement,
        Settings,
        ScanQR,
        Backup,
        Dapps,
        Storage,
        Upload,
        Download,
        Exit,
        Fatal
    }

    public enum PromptResult
    {
        Waiting,
        Failure,
        Success,
        Canceled
    }

    public enum AnimationDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }

    public enum ModalState
    {
        None,
        Message,
        Input,
        Password,
    }

    public enum TtrsNftSortMode // NFT-specific, TTRS-specific. Modes of NFT list sorting.
    {
        None,
        Number_Date,
        Date_Number,
        Type_Number_Date,
        Type_Date_Number,
        Type_Rarity
    }
    public enum NftSortMode // Modes of NFT list sorting.
    {
        None,
        Name,
        Number_Date,
        Date_Number
    }

    public enum SortDirection // Direction of sorting, used in NFT list sorting.
    {
        None,
        Ascending,
        Descending
    }

    public enum ttrsNftType // NFT-specific, TTRS-specific. Types of TTRS NFTs.
    {
        All,
        Vehicle,
        Part,
        License
    }

    public enum ttrsNftRarity // NFT-specific, TTRS-specific. Rarity classes of TTRS NFTs.
    {
        All = 0,
        Consumer = 1,
        Industrial = 2,
        Professional = 3,
        Collector = 4
    }

    public enum nftMinted // NFT-specific. Used in NFT filter, allows to select NFTs by mint date. All intervals are 'rolling'.
    {
        All,
        Last_15_Mins,
        Last_Hour,
        Last_24_Hours,
        Last_Week,
        Last_Month
    }

    public enum MoneyFormatType
    {
        Short,
        Standard,
        Long
    }
}
