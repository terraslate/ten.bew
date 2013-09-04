using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Server
{
    internal class CacheImpl : ICache
    {
        private readonly Lazy<Dictionary<string, CacheEntry>> _cache;

        public CacheImpl()
        {
            _cache = new Lazy<Dictionary<string, CacheEntry>>();
        }

        public CacheEntry GetCache(string cacheName)
        {
            cacheName = cacheName.ToLower();

            lock (_cache)
            {
                if (_cache.Value.ContainsKey(cacheName))
                {
                    return _cache.Value[cacheName];
                }
            }

            return null;
        }

        public bool GetOrCreateCache(string cacheName, out CacheEntry cacheEntry, Func<object> creationInitializer)
        {
            cacheName = cacheName.ToLower();

            bool rv = false;
            cacheEntry = null;

            lock (_cache)
            {
                if (_cache.Value.ContainsKey(cacheName) == false)
                {
                    cacheEntry = _cache.Value[cacheName] = new CacheEntry(creationInitializer());
                    rv = true;
                }
                else
                {
                    cacheEntry = _cache.Value[cacheName];
                }
            }

            return rv;
        }

        public IEnumerable<string> Keys
        { 
            get
            {
                return _cache.Value.Keys;
            }
        }

        public CacheEntry this[string cacheName]
        {
            get
            {
                return GetCache(cacheName);
            }
        }

    }


}
