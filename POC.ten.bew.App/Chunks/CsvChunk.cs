using ten.bew.Server;
using ten.bew.Server.Chunks;
using i.ten.bew;
using i.ten.bew.Messaging;
using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using POC.ten.bew.App.Messaging;

namespace POC.ten.bew.App.Chunks
{
    public class CsvChunk : ChunkBase
    {
        private string _sql;
        public class Creator : ChunkCreator
        {
            protected override ChunkBase Create(IServer server, dynamic parameters)
            {
                var rv = new CsvChunk(server, (string)parameters.sql);
                return rv;
            }
        }

        public CsvChunk(IServer server, string sql) 
            : base(server)
        {
            _sql = sql;
        }

        private static byte[] TextAreaStart = System.Text.Encoding.UTF8.GetBytes("<textarea>");
        private static byte[] TextAreaEnd = System.Text.Encoding.UTF8.GetBytes("</textarea>");
        private static byte[] Cancelled = System.Text.Encoding.UTF8.GetBytes("cancelled");
        private static byte[] NewLine = System.Text.Encoding.UTF8.GetBytes(Environment.NewLine);

        protected override async Task InternalSend(HttpClientImpl client, Stream outputStream)
        {
            CsvProcessor.Payload payload = new App.Messaging.CsvProcessor.Payload()
            {
                IsRequest = true,
                Sql = _sql
            };

            var payloadData = CsvProcessor.Payload.Serialize(payload);
            var peerInfo = Root.ServiceBusInstance.GetLocalService<IPeerManager>().FindRandomPeerForAddress("csvHandler");

            if(peerInfo == null)
            {
                throw new Exception("No peer was found.");
            }

            ServiceBusMessage message = new ServiceBusMessage("csvHandler", payloadData, DataFormatEnum.BinaryNet, peerInfo.Name);
            var replyReference = new Reference<ReplyTask<App.Messaging.CsvProcessor.Payload>>();
            Root.ServiceBusInstance.SendAsync(message, replyReference);

            await outputStream.WriteAsync(TextAreaStart, 0, TextAreaStart.Length);                
            
            byte[] timeData = System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString());
            await outputStream.WriteAsync(timeData, 0, timeData.Length);
            await outputStream.WriteAsync(NewLine, 0, NewLine.Length);

            try
            {
                var csvResult = await replyReference.Item;

                byte[] data = System.Text.Encoding.UTF8.GetBytes(replyReference.Item.ReplyMessage.Originator);
                await outputStream.WriteAsync(data, 0, data.Length);
                await outputStream.WriteAsync(NewLine, 0, NewLine.Length);

                foreach (var line in csvResult.Result)
                {
                    data = System.Text.Encoding.UTF8.GetBytes(line);
                    await outputStream.WriteAsync(data, 0, data.Length);
                    await outputStream.WriteAsync(NewLine, 0, NewLine.Length);
                }
            }
            catch(OperationCanceledException ex)
            {
                var data = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                outputStream.Write(data, 0, data.Length);
            }

            await outputStream.WriteAsync(TextAreaEnd, 0, TextAreaEnd.Length);
        }

    }
}
