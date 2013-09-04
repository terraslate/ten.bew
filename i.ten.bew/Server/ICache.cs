using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace i.ten.bew.Server
{
    public interface ICache
    {
        CacheEntry GetCache(string cacheName);

        bool GetOrCreateCache(string cacheName, out CacheEntry cacheEntry, Func<object> creationInitializer);

        IEnumerable<string> Keys { get; }

        CacheEntry this[string cacheName] { get; }
    }
}
