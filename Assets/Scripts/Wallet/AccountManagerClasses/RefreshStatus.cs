using System;

namespace Poltergeist
{
    public class RefreshStatus
    {
        // Balance
        public bool BalanceRefreshing;
        public DateTime LastBalanceRefresh;
        public Action BalanceRefreshCallback;
        // History
        public bool HistoryRefreshing;
        public DateTime LastHistoryRefresh;

        public override string ToString()
        {
            return $"BalanceRefreshing: {BalanceRefreshing}, LastBalanceRefresh: {LastBalanceRefresh}, HistoryRefreshing: {HistoryRefreshing}, LastHistoryRefresh: {LastHistoryRefresh}";
        }
    }
}
