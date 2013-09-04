using ten.bew.Server;
using ten.bew.Server.Chunks;
using i.ten.bew;
using i.ten.bew.Messaging;
using i.ten.bew.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace POC.ten.bew.App.Messaging
{
    public class CsvProcessor : MessageProcessorBase
    {
        public class Payload
        {
            public bool IsRequest;
            public string Sql;
            public List<string> Result;

            internal static byte[] Serialize(Payload graph)
            {
                var rv = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(graph));
                return rv;
            }

            internal static Payload Deserialize(byte[] serialized)
            {
                string jsonSerialized = System.Text.Encoding.UTF8.GetString(serialized);
                var rv = JsonConvert.DeserializeObject<Payload>(jsonSerialized);
                return rv;
            }
        }

        protected async override Task<object> InternalProcessMessageAsync(ServiceBusMessage message)
        {
            Payload payload = null;

            try
            {
                string jsonSerialized = System.Text.Encoding.UTF8.GetString(message.Data);

                payload = await JsonConvert.DeserializeObjectAsync<Payload>(jsonSerialized);

                if (payload.IsRequest)
                {
                    var responsePayload = new Payload()
                    {
                        IsRequest = false,
                        Sql = payload.Sql,
                        Result = new List<string>()
                    };

                    string connectionString = ConfigurationManager.ConnectionStrings["default"].ConnectionString;

                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();

                        using (SqlCommand command = new SqlCommand(payload.Sql, connection))
                        {
                            command.CommandType = System.Data.CommandType.Text;
                            command.CommandTimeout = 60;
                            command.CommandText = payload.Sql;

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
                                        var csv = String.Join(",", fieldsFlyWeight);
                                        responsePayload.Result.Add(csv);
                                        read = await reader.ReadAsync();
                                    }
                                }
                            }
                        }
                    }

                    var data = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(responsePayload));
                    var replyMessage = ServiceBusMessage.CreateReply(message, data, DataFormatEnum.BinaryNet);
                    Root.ServiceBusInstance.SendAsync(replyMessage);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return payload;
        }
    }
}
