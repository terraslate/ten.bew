using ten.bew.Caching;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace POC.ten.bew.App.Messaging
{
    class Echo : JSONMessageProcessorBase
    {
        public override bool CanHandleRequest(object inputRequest, out object outputRequest)
        {
            outputRequest = inputRequest;
            return true;
        }

        public override object HandleRequest(object request)
        {
            return request;
        }
    }
}
