using i.ten.bew.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace i.ten.bew.Messaging
{
    public interface IReplyInfo
    {
        ServiceBusMessage ReplyMessage
        {
            get;
            set;
        }

        object Payload
        {
            get;
            set;
        }

        CancellationTokenSource Cancellation
        {
            get;
        }

        Guid ForMessageId
        {
            get;
        }
    }
}
