using Poltergeist.PhantasmaLegacy.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace Poltergeist.PhantasmaLegacy.Storage.Context
{
    public class StorageKeyComparer : IEqualityComparer<StorageKey>
    {
        public bool Equals(StorageKey left, StorageKey right)
        {
            return left.keyData.SequenceEqual(right.keyData);
        }

        public int GetHashCode(StorageKey obj)
        {
            unchecked
            {
                return obj.keyData.Sum(b => b);
            }
        }
    }

    public struct StorageKey
    {
        public readonly byte[] keyData;

        public StorageKey(byte[] data)
        {
            this.keyData = data;
        }

        public override string ToString()
        {
            return Base16.Encode(keyData);
            //return ToHumanKey(keyData);
        }

        public override int GetHashCode()
        {
            return keyData.GetHashCode();
        }
    }
}
