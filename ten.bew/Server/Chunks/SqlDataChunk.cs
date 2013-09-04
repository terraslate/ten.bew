using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace ten.bew.Server.Chunks
{
    public abstract class SqlDataChunk : ChunkBase
    {
        string _connectionString;
        string _commandText;
        Lazy<CancellationTokenSource> _cancelToken = new Lazy<CancellationTokenSource>();

        public SqlDataChunk(IServer server, string connectionString, string commandText)
            : base(server)
        {
            _connectionString = connectionString;
            _commandText = commandText;
        }

        public CancellationToken CancelToken
        {
            get
            {
                return _cancelToken.Value.Token;;
            }
        }

        public override async Task Send(HttpClientImpl client, Stream redirectionStream)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync(CancelToken);

                using (SqlCommand command = new SqlCommand(_commandText, connection))
                {
                    command.CommandType = System.Data.CommandType.Text;
                    command.CommandTimeout = 60;

                    await StartReader(client, redirectionStream);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        bool read = await reader.ReadAsync();

                        if (read)
                        {
                            int recordIndex = 0;
                            object[] fieldsFlyWeight = new object[reader.FieldCount];

                            while (read != false)
                            {
                                var values = reader.GetValues(fieldsFlyWeight);
                                recordIndex++;
                                await ProcessRecord(client, redirectionStream, recordIndex, fieldsFlyWeight);
                                read = await reader.ReadAsync();
                            }
                        }
                    }

                    await StopReader(client, redirectionStream);
                }
            }
        }

        protected virtual async Task StopReader(HttpClientImpl client, Stream redirectionStream)
        {
        }

        protected virtual async Task StartReader(HttpClientImpl client, Stream redirectionStream)
        {
        }

        protected virtual async Task ProcessRecord(HttpClientImpl client, Stream redirectionStream, int recordIndex, object[] values)
        {

        }
    }
}
