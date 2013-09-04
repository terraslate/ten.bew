using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Caching
{
    [Serializable]
    public class CacheRequest
    {
        public CacheRequestTypeEnum RequestType;
        public byte[] Data;
        public ulong Key;
        public TimeSpan PutTimeToLive;
        public CacheEntryTypeEnum PutCacheEntryType;
    }
}
