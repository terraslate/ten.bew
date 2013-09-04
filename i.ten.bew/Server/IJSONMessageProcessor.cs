using i.ten.bew.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace i.ten.bew.Messaging
{
    public interface IJSONMessageProcessor : IMessageProcessor
    {
        bool CanHandleRequest(object inputRequest, out object outputRequest);

        object HandleRequest(object request);
    }
}
