using System.Collections.Generic;

namespace Phantasma.Bsc.Utils
{
    public static class BscRpcs
    {
        public static List<string> GetList(bool mainnet)
        {
            if (mainnet)
            {
                return new List<string> {
                    "https://bsc-dataseed.binance.org/",
                    "https://bsc-dataseed1.binance.org/",
                    "https://bsc-dataseed2.binance.org/",
                    "https://bsc-dataseed3.binance.org/",
                    "https://bsc-dataseed4.binance.org/",
                    "https://bsc-dataseed1.ninicoin.io/",
                    "https://bsc-dataseed2.ninicoin.io/",
                    "https://bsc-dataseed3.ninicoin.io/",
                    "https://bsc-dataseed4.ninicoin.io/",
                    "https://bsc-dataseed1.defibit.io/",
                    "https://bsc-dataseed2.defibit.io/",
                    "https://bsc-dataseed3.defibit.io/",
                    "https://bsc-dataseed4.defibit.io/"
                };
            }
            else
            {
                return new List<string> {
                    "https://data-seed-prebsc-1-s1.binance.org:8545/",
                    "https://data-seed-prebsc-1-s2.binance.org:8545/",
                    "https://data-seed-prebsc-2-s2.binance.org:8545/",
                    "https://data-seed-prebsc-1-s3.binance.org:8545/",
                    "https://data-seed-prebsc-2-s3.binance.org:8545/"
                };
            }
        }
    }

}
