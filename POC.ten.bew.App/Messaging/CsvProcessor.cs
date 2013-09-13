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
using System.Threading;
using System.Diagnostics;

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

        private static long _requestCount, _responseCount, _replyCount;

        public static long RequestCount
        {
            get
            {
                return Interlocked.Read(ref _requestCount);
            }
        }

        public static long ResponseCount
        {
            get
            {
                return Interlocked.Read(ref _responseCount);
            }
        }

        public static long ReplyCount
        {
            get
            {
                return Interlocked.Read(ref _replyCount);
            }
        }

        private static long _maxAge;

        public static double MaxAge
        {
            get
            {
                lock (typeof(CsvProcessor))
                {
                    return TimeSpan.FromTicks(_maxAge).TotalSeconds;
                }
            }
        }


        private static long _maxProcessingTime;

        public static double MaxProcessingTime
        {
            get
            {
                lock (typeof(CsvProcessor))
                {
                    return TimeSpan.FromTicks(_maxProcessingTime).TotalSeconds;
                }
            }
        }

        private static long _receivedMessage;

        public static double ReceivedMessage
        {
            get
            {
                return Interlocked.Read(ref _receivedMessage);
            }
        }

        protected async override Task<object> InternalProcessMessageAsync(ServiceBusMessage message)
        {
            Stopwatch watch = Stopwatch.StartNew();

            Interlocked.Increment(ref _receivedMessage);

            lock (typeof(CsvProcessor))
            {
                _maxAge = Math.Max(Math.Abs(message.Age.Ticks), _maxAge);
            }

            Payload payload = null;

            try
            {
                string jsonSerialized = System.Text.Encoding.UTF8.GetString(message.Data);

                payload = await JsonConvert.DeserializeObjectAsync<Payload>(jsonSerialized);

                if (payload.IsRequest)
                {
                    Interlocked.Increment(ref _requestCount);

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

                    if (message.OriginatorRequiresReply)
                    {
                        var data = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(responsePayload));
                        var replyMessage = ServiceBusMessage.CreateReply(message, data, DataFormatEnum.BinaryNet);
                        Interlocked.Increment(ref _replyCount);
                        Root.ServiceBusInstance.SendAsync(replyMessage);
                    }
                }
                else
                {
                    Interlocked.Increment(ref _responseCount);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                lock (typeof(CsvProcessor))
                {
                    _maxProcessingTime = Math.Max((watch.Elapsed).Ticks, _maxProcessingTime);
                }

                watch.Stop();
            }

            return payload;
        }
    }
}
