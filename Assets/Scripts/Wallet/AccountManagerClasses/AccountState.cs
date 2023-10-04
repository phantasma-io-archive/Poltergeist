using System.Collections.Generic;
using Phantasma.Core.Types;
using Phantasma.SDK;

namespace Poltergeist
{
    public class AccountState
    {
        public PlatformKind platform;
        public string name;
        public string address;
        public Balance[] balances;
        public AccountFlags flags;
        public Timestamp stakeTime;

        public Archive[] archives;
        public string avatarData;
        public uint availableStorage;
        public uint usedStorage;
        public uint totalStorage => availableStorage + usedStorage;

        public Dictionary<string, string> dappTokens = new Dictionary<string, string>();

        public decimal GetAvailableAmount(string symbol)
        {
            for (int i = 0; i < balances.Length; i++)
            {
                var entry = balances[i];
                if (entry.Symbol == symbol)
                {
                    return entry.Available;
                }
            }

            return 0;
        }

        public void RegisterDappToken(string dapp, string token)
        {
            dappTokens[dapp] = token;
        }
    }
}
