using ten.bew.Server;
using ten.bew.Server.Chunks;
using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace POC.ten.bew.App.Chunks
{
    public class BusinessChunk : SqlDataChunk
    {
        private static readonly byte[] MACHINE_NAME_BYTES = System.Text.Encoding.UTF8.GetBytes(Environment.MachineName);
        private static readonly byte[] TABLE = System.Text.Encoding.UTF8.GetBytes("<table>");
        private static readonly byte[] TABLE_END = System.Text.Encoding.UTF8.GetBytes("</table>");
        private static readonly byte[] TR = System.Text.Encoding.UTF8.GetBytes("<tr>");
        private static readonly byte[] TD = System.Text.Encoding.UTF8.GetBytes("<td>");
        private static readonly byte[] TR_END = System.Text.Encoding.UTF8.GetBytes("</tr>");
        private static readonly byte[] TD_END = System.Text.Encoding.UTF8.GetBytes("</td>");

        public class Creator : ChunkCreator
        {
            protected override ChunkBase Create(IServer server, dynamic parameters)
            {
                return new BusinessChunk(
                    server, 
                    (string)parameters.connectionString, 
                    (string)parameters.id
                );
            }
        }

        public BusinessChunk(IServer server, string connectionString, string id) 
            : base(server, connectionString, string.Format("SELECT * FROM Member WHERE MemberID = {0}", id))
        {
        }

        protected override async Task StartReader(HttpClientImpl client, System.IO.Stream redirectionStream)
        {
            redirectionStream = (redirectionStream ?? client.Context.Response.OutputStream);
            await redirectionStream.WriteAsync(TABLE, 0, TABLE.Length);
        }

        protected override async Task StopReader(HttpClientImpl client, System.IO.Stream redirectionStream)
        {
            redirectionStream = (redirectionStream ?? client.Context.Response.OutputStream);  
            await redirectionStream.WriteAsync(TABLE_END, 0, TABLE_END.Length);
        }

        protected override async Task ProcessRecord(HttpClientImpl client, System.IO.Stream redirectionStream, int recordIndex, object[] values)
        {
            redirectionStream = (redirectionStream ?? client.Context.Response.OutputStream);   
         
            await redirectionStream.WriteAsync(TR, 0, TR.Length);

            await redirectionStream.WriteAsync(TD, 0, TD.Length);
            await redirectionStream.WriteAsync(MACHINE_NAME_BYTES, 0, MACHINE_NAME_BYTES.Length);
            await redirectionStream.WriteAsync(TD_END, 0, TD_END.Length);

            foreach (var value in values)
            {
                await redirectionStream.WriteAsync(TD, 0, TD.Length);
                var valueBytes = System.Text.Encoding.UTF8.GetBytes(value.ToString());
                await redirectionStream.WriteAsync(valueBytes, 0, valueBytes.Length);
                await redirectionStream.WriteAsync(TD_END, 0, TD_END.Length);
            }   
            
            await redirectionStream.WriteAsync(TR_END, 0, TR_END.Length);
        }
    }
}
