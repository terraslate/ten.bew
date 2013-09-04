using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Server
{
    public class HttpClientImpl : IClient<HttpListenerContext>
    {  
        private HttpListenerContext _context;

        internal long StartTicks;
        internal long StopTicks;

        public HttpClientImpl(HttpListenerContext context)
        {
            _context = context;
        }
        public HttpListenerContext Context
        {
            get
            {
                return _context;
            }
        }

        public long TimetoLastByte
        {
            get
            {
                return StopTicks - StartTicks;
            }
        }
    }
}
