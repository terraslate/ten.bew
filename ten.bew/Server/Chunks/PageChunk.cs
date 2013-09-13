using i.ten.bew.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ten.bew.Server.Chunks
{
    public class PageChunk : FileChunk
    {
        private HttpClientImpl _client;
        private List<ChunkBase> _chunks;
        private string _pageName;

        public PageChunk(IServer server, string pageName)
            : base(server)
        {
            _pageName = pageName;
        }

        public virtual bool IsReusable
        {
            get
            {
                return true;
            }
        }


        public override async Task Send(HttpClientImpl client, Stream redirectionStream)
        {
            try
            {
                client.Context.Response.AppendHeader("Content-Type", "text/html");

                if (IsReusable)
                {
                    string cacheKey = "__PAGE____" + (_pageName ?? Server.GetFileName(client.Context.Request.RawUrl));

                    CacheEntry entry;

                    bool created = Server.Cache.GetOrCreateCache(cacheKey, out entry, () => { return new List<ChunkBase>(); });

                    if (created == false)
                    {
                        entry.TokenSource.Token.WaitHandle.WaitOne();
                        _chunks = entry.GetEntry<List<ChunkBase>>();
                    }
                    else
                    {
                        try
                        {
                            _chunks = entry.GetEntry<List<ChunkBase>>();
                            await Render(client);
                        }
                        finally
                        {
                            entry.TokenSource.Cancel();
                        }
                    }
                }
                else
                {
                    _chunks = new List<ChunkBase>();
                    await Render(client);
                }

                foreach (var chunk in _chunks)
                {
                    await chunk.Send(client, redirectionStream);
                }
            }
            finally
            {
                client.Context.Response.OutputStream.FlushAsync().ContinueWith((t) =>
                {
                    if (SendType == SendTypeEnum.CloseStream)
                    {
                        client.Context.Response.OutputStream.Close();
                    }
                }
                );
            }
        }

        public class PageDescription
        {
            public class ChunkDescription
            {
                public string Name;
                public dynamic Config;
            }

            public Dictionary<string, string> Namespaces;
            public List<ChunkDescription> Chunks;
        }

        private async Task Render(HttpClientImpl client)
        {
            FileChunk fileChunk = new FileChunk(Server);
            fileChunk.SendType = SendTypeEnum.None;

            PageDescription description = null;

            using (MemoryStream fileContents = new MemoryStream())
            {
                await fileChunk.Send(client, fileContents);
                fileContents.Position = 0;

                using (StreamReader reader = new StreamReader(fileContents))
                {
                    var data = await reader.ReadToEndAsync();
                    description = await JsonConvert.DeserializeObjectAsync<PageDescription>(data);
                }
            }

            BuildChunks(Server, _chunks, description);
        }

        private static readonly char[] ColonSplitter = ":".ToCharArray();
        private static readonly char[] CommaSplitter = ",".ToCharArray();

        internal static void BuildChunks(IServer server, IList<ChunkBase> chunks, PageDescription description)
        {
            foreach (var chunkDescription in description.Chunks)
            {
                var nameParts = chunkDescription.Name.Split(ColonSplitter, 2);
                var ns = description.Namespaces[nameParts[0]];
                var nsParts = ns.Split(CommaSplitter, 2);
                nsParts[0] += "." + nameParts[1] + "Chunk+Creator";

                string typeName = string.Join(", ", nsParts);

                Type type = Type.GetType(typeName);

                var chunkCreatorWrap = Activator.CreateInstance(type);
                ChunkCreator chunkCreator = (ChunkCreator)chunkCreatorWrap;
                ChunkBase chunk = chunkCreator.Create(server, chunkDescription.Config);
                chunk.SendType = SendTypeEnum.None;
                chunks.Add(chunk);
            }


        }
    }
}
