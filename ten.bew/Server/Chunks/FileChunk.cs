using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Server.Chunks
{
    public class FileChunk : ChunkBase
    {
        private string _rawUrl;
        public FileChunk(IServer server) 
            : base(server)
        {
        }

        public FileChunk(IServer server, string rawUrl) 
            : base(server)
        {
            _rawUrl = rawUrl;
        }

        public class Creator : ChunkCreator
        {
            protected internal override ChunkBase Create(IServer server, dynamic parameter)
            {
                return new FileChunk(server, parameter);
            }
        }

        public override async Task Send(HttpClientImpl client, Stream redirectionStream)
        {
            var context = client.Context;
            string cacheName = (_rawUrl ?? context.Request.RawUrl);
            CacheEntry cacheEntry = Server.Cache.GetCache(cacheName);
            
            if(redirectionStream == null)
            {
                redirectionStream = client.Context.Response.OutputStream;
            }

            if (cacheEntry != null)
            {
                cacheEntry.TokenSource.Token.WaitHandle.WaitOne();

                for (int i = 0; i < cacheEntry.GetEntry<List<byte[]>>().Count; i++)
                {
                    byte[] byteArray = cacheEntry.GetEntry<List<byte[]>>()[i];
                    await redirectionStream.WriteAsync(byteArray, 0, byteArray.Length);
                }
            }
            else
            {
                string diskName = Server.GetFileName(cacheName);

                if (File.Exists(diskName))
                {
                    bool created = Server.Cache.GetOrCreateCache(cacheName, out cacheEntry, () => { return new List<byte[]>(); });

                    if (created == false)
                    {
                        bool timeOut = cacheEntry.TokenSource.Token.WaitHandle.WaitOne();

                        for (int i = 0; i < cacheEntry.GetEntry<List<byte[]>>().Count; i++)
                        {
                            byte[] byteArray = cacheEntry.GetEntry<List<byte[]>>()[i];
                            await redirectionStream.WriteAsync(byteArray, 0, byteArray.Length);
                        }
                    }
                    else
                    {
                        try
                        {
                            using (FileStream fs = new FileStream(diskName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                byte[] buffer = new byte[1024];
                                int read = 0;

                                while ((read = await fs.ReadAsync(buffer, 0, buffer.Length)) != 0)
                                {
                                    byte[] bufferEntry = new byte[read];
                                    Buffer.BlockCopy(buffer, 0, bufferEntry, 0, read);
                                    cacheEntry.GetEntry<List<byte[]>>().Add(bufferEntry);

                                    await redirectionStream.WriteAsync(bufferEntry, 0, bufferEntry.Length);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            cacheEntry.TokenSource.Cancel();
                        }
                    }
                }
                else
                {
                    byte[] buffer = Server.GetStatusCode(404);
                    await redirectionStream.WriteAsync(buffer, 0, buffer.Length);
                }
            }

            await redirectionStream.FlushAsync();

            if (SendType == SendTypeEnum.CloseStream)
            {
                redirectionStream.Close();
            }
        }
    }
}
