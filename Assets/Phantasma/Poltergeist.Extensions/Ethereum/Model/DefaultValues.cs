﻿using Poltergeist.PhantasmaLegacy.Ethereum.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace Poltergeist.PhantasmaLegacy.Ethereum.Model
{
    public class DefaultValues
    {

        public static DefaultValues Current { get; } = new DefaultValues();

        public static byte[] EMPTY_BYTE_ARRAY = new byte[0];
        public static readonly byte[] ZERO_BYTE_ARRAY = { 0 };
        public static readonly byte[] EMPTY_DATA_HASH = Sha3Keccack.Current.CalculateHash(EMPTY_BYTE_ARRAY);
        public static readonly byte[] EMPTY_TRIE_HASH = Sha3Keccack.Current.CalculateHash(RLP.RLP.EncodeElement(EMPTY_BYTE_ARRAY));

       
    }
}
