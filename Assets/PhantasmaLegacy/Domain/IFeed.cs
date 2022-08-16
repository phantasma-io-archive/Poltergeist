using Poltergeist.PhantasmaLegacy.Cryptography;

namespace Poltergeist.PhantasmaLegacy.Domain
{
    public enum FeedMode
    {
        First,
        Last,
        Max,
        Min,
        Average
    }

    public interface IFeed
    {
        string Name { get;  }
        Address Address { get;  }
        FeedMode Mode { get;  }
    }
}
