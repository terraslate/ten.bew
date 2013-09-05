using i.ten.bew.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace i.ten.bew.Messaging
{
    /// <summary>
    /// Message processors run anywhere, on any machine. They don't have access to the http request.
    /// </summary>
    public interface IJSONMessageProcessor : IMessageProcessor
    {
        bool CanHandleRequest(object inputRequest, out object outputRequest);

        object HandleRequest(object request);
    }
}
