using i.ten.bew;
using i.ten.bew.Messaging;
using i.ten.bew.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace i.ten.bew.Messaging
{
    public abstract class MessageProcessorBase : IMessageProcessor
    {

        public object ProcessMessage(ServiceBusMessage message)
        {
            var task = ProcessMessageAsync(message);
            task.Wait();
            return task.Result;
        }

        public async Task<object> ProcessMessageAsync(ServiceBusMessage message)
        {
            object rv = null;
            rv = await InternalProcessMessageAsync(message);
            return rv;
        }

        protected async virtual Task<object> InternalProcessMessageAsync(ServiceBusMessage message)
        {
            return null;
        }
    }
}
