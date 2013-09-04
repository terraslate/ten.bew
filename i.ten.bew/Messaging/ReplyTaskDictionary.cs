using i.ten.bew;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace i.ten.bew.Messaging
{
    public class ReplyTaskDictionary : Dictionary<Guid, Task>
    {
        private CancellationTokenSource _cancellation = new CancellationTokenSource();

        public ReplyTask<P> AddForMessage<P>(ServiceBusMessage forMessage) where P : class
        {
            ReplyTask<P> rv = new ReplyTask<P>(forMessage.MessageId, _cancellation);
            Add(forMessage.MessageId, rv);
            return rv;
        }

        public void Cancel()
        {
            _cancellation.Cancel();
        }
    }
}
