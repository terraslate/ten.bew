using ten.bew.Caching;
using i.ten.bew;
using i.ten.bew.Messaging;
using i.ten.bew.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Server.Chunks
{
    public class CachingChunk : ChunkBase
    {
        public class Creator : ChunkCreator
        {
            protected internal override ChunkBase Create(IServer server, dynamic parameters)
            {
                return new CachingChunk(server);
            }
        }

        public CachingChunk(IServer server) 
            : base(server)
        {        
        }

        protected override async Task InternalSend(HttpClientImpl client, System.IO.Stream outputStream)
        {
            int read = 0;

            List<byte[]> dataSet = new List<byte[]>();

            byte[] buffer = new byte[1024];
            int totalRead = 0;

            while ((read = await client.Context.Request.InputStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                if (read > 0)
                {
                    totalRead += read;
                    byte[] data = new byte[read];
                    Buffer.BlockCopy(buffer, 0, data, 0, read);
                    dataSet.Add(data);
                }
            }

            byte[] packet = new byte[totalRead];
            int packetOffset = 0;

            foreach(var item in dataSet)
            {
                Buffer.BlockCopy(item, 0, packet, packetOffset, item.Length);
                packetOffset += item.Length;
            }

            string jsonCacheRequest = System.Text.Encoding.UTF8.GetString(packet);
            var cacheRequest = await JsonConvert.DeserializeObjectAsync<CacheRequest>(jsonCacheRequest);

            var localHandler = (IJSONMessageProcessor)Root.ServiceBusInstance.GetMessageProcessor(Program.CACHING_MESSAGE_PROCESSOR_KEY);
            CacheResponse cacheResponse = null;

            object outputRequest;

            if (localHandler.CanHandleRequest(cacheRequest, out outputRequest))
            {
                cacheResponse = (CacheResponse)localHandler.HandleRequest(outputRequest);
            }
            else
            {
                var serializer = Root.ServiceBusInstance.GetLocalService<ISerializer>();
                var serializedCacheRequest = serializer.Serialize(cacheRequest);

                ServiceBusMessage message = new ServiceBusMessage("__CachingMessageProcessor", serializedCacheRequest, DataFormatEnum.BinaryNet);

                var responseReference = new Reference<ReplyTask<CacheResponse>>();
                await Root.ServiceBusInstance.SendAsync(message, responseReference);
                cacheResponse = await responseReference.Item;
            }

            cacheResponse.ErrorDescription = new string('A', 160000);

            var serializedJson = await JsonConvert.SerializeObjectAsync(cacheResponse);

            var utf8Encoding = new System.Text.UTF8Encoding(false);
            byte[] encodingBuffer = new byte[2048];

            int numCharsToRead = Math.Min(1024, serializedJson.Length);
            int stringPosition = 0;
            int totalCharsRead = 0;

            while(stringPosition < serializedJson.Length)
            {
                numCharsToRead = Math.Min(serializedJson.Length - stringPosition, 1024);
                int encodedBytes = utf8Encoding.GetBytes(serializedJson, stringPosition, numCharsToRead, encodingBuffer, 0);
                stringPosition += numCharsToRead;
                await outputStream.WriteAsync(encodingBuffer, 0, encodedBytes);
            }
        }
    }
}
