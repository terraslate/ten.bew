using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Caching
{   
    public enum CacheEntryTypeEnum : byte
    {
        Expiry,
        SlidingExpiry,
    }
}
