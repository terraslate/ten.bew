using i.ten.bew;
using i.ten.bew.Messaging;
using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ten.bew.Messaging;

namespace ten.bew.Caching
{
    public abstract class JSONMessageProcessorBase : i.ten.bew.Messaging.MessageProcessorBase, IJSONMessageProcessor
    {
        public const int SUCCESS = 0;
        public const int ERRORCODE_NOTFOUND_OR_EXPIRED = 1;
        public const int ERROR_UNRECOGNIZED_REQUEST = 2;
        public const int ERROR_UNKNOWN = 3;

        protected override async Task<object> InternalProcessMessageAsync(i.ten.bew.Messaging.ServiceBusMessage message)
        {
            ISerializer serializer = Root.ServiceBusInstance.GetLocalService<ISerializer>();

            object inputRequest = serializer.Deserialize(message.Data);
            object rv = null;

            object outputRequest;

            bool canHandleRequest = CanHandleRequest(inputRequest, out outputRequest);

            if (canHandleRequest)
            {
                rv = HandleRequest(outputRequest);

                if (rv != null)
                {
                    var payload = serializer.Serialize(rv);
                    ServiceBusMessage replyMessage = ServiceBusMessage.CreateReply(message, payload, DataFormatEnum.JSON);
                    Root.ServiceBusInstance.SendAsync(replyMessage);
                }
            }

            return rv;
        }

        public abstract bool CanHandleRequest(object inputRequest, out object outputRequest);

        public abstract object HandleRequest(object request);
    }
}
