using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Caching
{
    public enum CacheRequestTypeEnum : byte
    {
        Get = 0x01, // gets the specified item
        Put = 0x02, // puts the specified item
        Delete = 0x04, // deletes the specified item
        Lock = 0x08, // locks the specified item
        Unlock = 0x10, // unlocks the specified item
        Single = 0x20, // makes this server the single copy
        Multi = 0x40, // makes the server push its copy to all other servers
    }
}
