using System;

namespace Phantasma.Core.Storage.Context
{
    public interface IStorageCollection
    {
        byte[] BaseKey { get; }
        StorageContext Context { get; }
    }

    public class StorageException: Exception
    {
        public StorageException(string msg): base(msg)
        {

        }
    }
}
