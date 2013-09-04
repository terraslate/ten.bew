using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace i.ten.bew.Server
{
    public class CacheEntry
    {
        public readonly object _entry;
        public readonly CancellationTokenSource TokenSource;
        public readonly DateTime Created;

        public CacheEntry(object entry)
        {
            _entry = entry;
            TokenSource = new CancellationTokenSource();
            Created = DateTime.UtcNow;
        }

        public object Entry
        {
            get
            {
                return _entry;
            }
        }

        public T GetEntry<T>()
        {
            return (T)Entry;
        }
    }
}


