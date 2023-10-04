using System.Collections.Generic;
using Phantasma.SDK;

namespace Poltergeist
{
    public static class AccountFlagsExtensions
    {
        public static List<PlatformKind> Split(this PlatformKind kind)
        {
            var list = new List<PlatformKind>();
            foreach (var platform in AccountManager.AvailablePlatforms)
            {
                if (kind.HasFlag(platform))
                {
                    list.Add(platform);
                }
            }
            return list;
        }

        public static PlatformKind GetTransferTargets(this PlatformKind kind, Token token)
        {
            if (!token.IsSwappable())
            {
                return kind;
            }

            PlatformKind targets;

            switch (kind)
            {
                case PlatformKind.Phantasma:
                    targets = PlatformKind.Phantasma;
                    targets |= Tokens.HasSwappableToken(token.symbol, PlatformKind.Neo) ? PlatformKind.Neo : PlatformKind.None;
                    targets |= Tokens.HasSwappableToken(token.symbol, PlatformKind.Ethereum) ? PlatformKind.Ethereum : PlatformKind.None;
                    targets |= Tokens.HasSwappableToken(token.symbol, PlatformKind.BSC) ? PlatformKind.BSC : PlatformKind.None;
                    return targets;

                case PlatformKind.Neo:
                    targets = PlatformKind.Neo;
                    targets |= Tokens.HasSwappableToken(token.symbol, PlatformKind.Phantasma) ? PlatformKind.Phantasma : PlatformKind.None;
                    return targets;

                case PlatformKind.Ethereum:
                    targets = PlatformKind.Ethereum;
                    targets |= Tokens.HasSwappableToken(token.symbol, PlatformKind.Phantasma) ? PlatformKind.Phantasma : PlatformKind.None;
                    return targets;

                case PlatformKind.BSC:
                    targets = PlatformKind.BSC;
                    targets |= Tokens.HasSwappableToken(token.symbol, PlatformKind.Phantasma) ? PlatformKind.Phantasma : PlatformKind.None;
                    return targets;

                default:
                    return PlatformKind.None;
            }
        }
        public static bool ValidateTransferTarget(this PlatformKind kind, Token token, PlatformKind targetKind)
        {
            var targets = kind.GetTransferTargets(token);
            return targets.HasFlag(targetKind);
        }
    }
}