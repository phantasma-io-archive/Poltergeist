using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Numerics;
using System;

namespace Poltergeist
{    
    public struct Account
    {
        public static readonly int MaxPasswordLength = 20;

        public string name;
        public string platform;
        public string key;
        public string password;
        public string misc;

        public override string ToString()
        {
            return $"{name.ToUpper()} [{platform}]";
        }
    }

    public struct Balance
    {
        public string symbol;
        public decimal amount;
        public string chain;
    }

    public class AccountManager : MonoBehaviour
    {
        public Account[] Accounts { get; private set; }

        public static AccountManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        // Start is called before the first frame update
        void Start()
        {
            var wallets = PlayerPrefs.GetString("polterwallet", "");

            if (!string.IsNullOrEmpty(wallets))
            {
                var bytes = Base16.Decode(wallets);
                Accounts = Serialization.Unserialize<Account[]>(bytes);
            }
            else
            {
                Accounts = new Account[] { new Account() { name = "demo", platform = "phantasma", key = "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25", password = "lol", misc = "" } };
            }
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void FetchBalances(Account account, Action<Balance[]> callback)
        {
            callback(new Balance[]
            {
                new Balance() { symbol = "SOUL", amount = 100, chain = "main"},
                new Balance() { symbol = "KCAL", amount = 200, chain = "main"}
            });
        }
    }
}
