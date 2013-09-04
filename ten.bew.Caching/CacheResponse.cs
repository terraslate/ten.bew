using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Caching
{
    [Serializable]
    public class CacheResponse
    {
        public CacheResponse()
        {
            FromServer = System.Environment.MachineName;
        }

        public ulong Key;
        public byte[] Data;

        public int ErrorCode;
        public string ErrorDescription;

        public string FromServer;
    }
}
