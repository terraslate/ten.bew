using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Server.Chunks
{
    public class StaticChunk : ChunkBase
    {
        public class Creator : ChunkCreator
        {
            protected internal override ChunkBase Create(IServer server, dynamic parameter)
            {
                return new StaticChunk(server, new List<byte[]>() { System.Text.Encoding.UTF8.GetBytes((string)(parameter.text)) });
            }
        }

        private List<byte[]> _staticData;

        public StaticChunk(IServer server, List<byte[]> staticData)
            : base(server)
        {
            _staticData = staticData;
        }

        public override async Task Send(HttpClientImpl client, Stream redirectionStream)
        {
            redirectionStream = redirectionStream ?? client.Context.Response.OutputStream;

            foreach(var data in _staticData)
            {
                await redirectionStream.WriteAsync(data,0, data.Length);
            }

            await redirectionStream.FlushAsync();

            if (SendType == SendTypeEnum.CloseStream)
            {
                redirectionStream.Close();
            }
        }
    }
}
