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
    internal class RESTChunk : ChunkBase
    {
        public class Creator : ChunkCreator
        {
            protected internal override ChunkBase Create(IServer server, dynamic parameters)
            {
                return new RESTChunk(server);
            }
        }

        public RESTChunk(IServer server)
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

            foreach (var item in dataSet)
            {
                Buffer.BlockCopy(item, 0, packet, packetOffset, item.Length);
                packetOffset += item.Length;
            }

            string serializedJsonRequest = System.Text.Encoding.UTF8.GetString(packet);

            JSONMessageProcessorBase.Payload payload = new JSONMessageProcessorBase.Payload()
            {
                JSON = serializedJsonRequest,
                Cookies = client.Context.Request.Cookies,
                AcceptTypes = client.Context.Request.AcceptTypes,
                Method = client.Context.Request.HttpMethod,
                Url = client.Context.Request.Url,
            };

            if(client.Context.User != null && client.Context.User.Identity != null) 
            {
                payload.User = client.Context.User.Identity.Name;
                payload.IsAuthenticated = client.Context.User.Identity.IsAuthenticated;
            }

            var parameters = client.Context.Request.RawUrl.Split('/');
            string action = parameters[1];

            var localHandler = (IJSONMessageProcessor)Root.ServiceBusInstance.GetMessageProcessor(action);
            object responseObject = null;
            object outputRequest;


            if (localHandler.CanHandleRequest(payload, out outputRequest))
            {
                responseObject = localHandler.HandleRequest(outputRequest);
            }
            else
            {
                ServiceBusMessage message = new ServiceBusMessage(action, packet, DataFormatEnum.JSON);
                var responseReference = new Reference<ReplyTask<CacheResponse>>();
                await Root.ServiceBusInstance.SendAsync(message, responseReference);
                responseObject = await responseReference.Item;
            }

            string serializedResponseObject = null;

            if(responseObject is string)
            {
                serializedResponseObject = (string)responseObject;
            }
            else
            {
                serializedResponseObject = await JsonConvert.SerializeObjectAsync(responseObject);
            }

            var utf8Encoding = new System.Text.UTF8Encoding(false);
            byte[] encodingBuffer = new byte[2048];

            int numCharsToRead = Math.Min(1024, serializedResponseObject.Length);
            int stringPosition = 0;
            int totalCharsRead = 0;

            while (stringPosition < serializedResponseObject.Length)
            {
                numCharsToRead = Math.Min(serializedResponseObject.Length - stringPosition, 1024);
                int encodedBytes = utf8Encoding.GetBytes(serializedResponseObject, stringPosition, numCharsToRead, encodingBuffer, 0);
                stringPosition += numCharsToRead;
                await outputStream.WriteAsync(encodingBuffer, 0, encodedBytes);
            }
        }
    }
}
