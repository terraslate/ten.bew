using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Server.Chunks
{
    public abstract class ChunkBase
    {
        private readonly IServer _server;
        private SendTypeEnum _sendType = SendTypeEnum.CloseStream;

        public ChunkBase(IServer server)
        {
            _server = server;
        }

        internal SendTypeEnum SendType
        {
            get
            {
                return _sendType;
            }
            set
            {
                _sendType = value;
            }
        }

        internal protected IServer Server
        {
            get
            {
                return _server;
            }
        }

        public virtual async Task Send(HttpClientImpl client, Stream redirectionStream)
        {
            redirectionStream = redirectionStream ?? client.Context.Response.OutputStream;

            await InternalSend(client, redirectionStream);

            await redirectionStream.FlushAsync();

            if (SendType == SendTypeEnum.CloseStream)
            {
                redirectionStream.Close();
            }
        }

        protected virtual async Task InternalSend(HttpClientImpl client, Stream outputStream)
        {

        }
    }
}
