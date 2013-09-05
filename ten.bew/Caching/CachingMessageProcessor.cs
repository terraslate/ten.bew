using i.ten.bew;
using i.ten.bew.Messaging;
using i.ten.bew.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Caching
{
    class CachingMessageProcessor : i.ten.bew.Messaging.MessageProcessorBase, IJSONMessageProcessor
    {
        public const int SUCCESS = 0;
        public const int ERRORCODE_NOTFOUND_OR_EXPIRED = 1;
        public const int ERROR_UNRECOGNIZED_REQUEST = 2;
        public const int ERROR_UNKNOWN = 3;

        private Dictionary<ulong, CacheEntry> _items;
        private ulong _shardStart = Program.Config.Caching.ShardStart;
        private ulong _shardEnd = Program.Config.Caching.ShardEnd;

        public CachingMessageProcessor()
        {
            _items = new Dictionary<ulong, CacheEntry>();
        }

        internal ulong ShardStart
        {
            get
            {
                return _shardStart;
            }
        }

        internal ulong ShardEnd
        {
            get
            {
                return _shardEnd;
            }
        }

        protected override async Task<object> InternalProcessMessageAsync(i.ten.bew.Messaging.ServiceBusMessage message)
        {
            ISerializer serializer = Root.ServiceBusInstance.GetLocalService<ISerializer>();
            object rv = null;

            switch (message.DataFormat)
            {
                case DataFormatEnum.BinaryNet:
                    rv = serializer.Deserialize(message.Data);
                    break;

                case DataFormatEnum.JSON:

                    string json = System.Text.Encoding.UTF8.GetString(message.Data);

                    if (json.IndexOf("RequestType") > 0)
                    {
                        rv = JsonConvert.DeserializeObject<CacheRequest>(json);
                    }
                    else
                    {
                        rv = JsonConvert.DeserializeObject<CacheResponse>(json);
                    }

                    break;

                default:
                    throw new Exception("Unrecognized format enum.");
            }

            if (rv is CacheRequest)
            {
                CacheRequest request = (CacheRequest)rv;

                object outputRequest;

                bool canHandleRequest = CanHandleRequest(request, out outputRequest);

                if (canHandleRequest)
                {
                    rv = HandleRequest(request);

                    if (rv != null)
                    {
                        var payload = serializer.Serialize(rv);
                        ServiceBusMessage replyMessage = ServiceBusMessage.CreateReply(message, payload, DataFormatEnum.BinaryNet);
                        Root.ServiceBusInstance.SendAsync(replyMessage);
                    }
                }
            }
            else if (rv is CacheResponse)
            {
            }
            else
            {
                rv = null;
            }

            return rv;
        }

        public object HandleRequest(object jsonRequest)
        {
            CacheResponse rv = null;
            CacheRequest request = null;

            try
            {
                request = (CacheRequest)jsonRequest;

                switch (request.RequestType)
                {
                    case CacheRequestTypeEnum.Get:

                        CacheEntry getEntry = null;

                        lock (_items)
                        {
                            getEntry = _items.ContainsKey(request.Key) ? _items[request.Key] : null;
                        }

                        if (getEntry != null && DateTime.UtcNow > getEntry.Expires)
                        {
                            rv = new CacheResponse()
                            {
                                Key = request.Key,
                                Data = getEntry.Data,
                                ErrorCode = SUCCESS
                            };
                        }
                        else
                        {
                            rv = new CacheResponse()
                            {
                                Key = request.Key,
                                Data = null,
                                ErrorCode = ERRORCODE_NOTFOUND_OR_EXPIRED
                            };
                        }

                        break;

                    case CacheRequestTypeEnum.Put:

                        var putEntry = new CacheEntry(request.PutTimeToLive, request.PutCacheEntryType);
                        putEntry.Data = request.Data;

                        lock (_items)
                        {
                            _items[request.Key] = putEntry;
                        }

                        rv = new CacheResponse()
                        {
                            Key = request.Key,
                            Data = null,
                            ErrorCode = SUCCESS
                        };

                        break;

                    case CacheRequestTypeEnum.Delete:

                        lock (_items)
                        {
                            if (_items.ContainsKey(request.Key))
                            {
                                _items.Remove(request.Key);
                            }
                        }

                        rv = new CacheResponse()
                        {
                            Key = request.Key,
                            Data = null,
                            ErrorCode = SUCCESS
                        };

                        break;

                    default:

                        rv = new CacheResponse()
                        {
                            Key = request.Key,
                            Data = null,
                            ErrorCode = ERROR_UNRECOGNIZED_REQUEST
                        };

                        break;
                }
            }
            catch (Exception ex)
            {
                rv = new CacheResponse()
                {
                    Key = request.Key,
                    Data = null,
                    ErrorCode = ERROR_UNKNOWN,
                    ErrorDescription = ex.Message
                };
            }

            return rv;
        }

        public bool CanHandleRequest(object inputRequest, out object outputRequest)
        {
            bool rv = false;
            outputRequest = null;

            if (inputRequest is ten.bew.Caching.JSONMessageProcessorBase.Payload)
            {
                inputRequest = ((ten.bew.Caching.JSONMessageProcessorBase.Payload)inputRequest).JSON;
            }

            if (inputRequest is string)
            {
                inputRequest = JsonConvert.DeserializeObject<CacheRequest>((string)inputRequest);
            }

            if (inputRequest is CacheRequest)
            {
                var request = (CacheRequest)inputRequest;
                rv = (request.Key >= ShardStart && request.Key <= ShardEnd);
                outputRequest = request;
            }

            return rv;
        }
    }
}
