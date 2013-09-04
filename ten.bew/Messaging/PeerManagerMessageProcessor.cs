using i.ten.bew;
using i.ten.bew.Messaging;
using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Server
{
    class PeerManagerMessageProcessor : MessageProcessorBase
    {
        private readonly IPeerManager _peerManagerInstance;

        public PeerManagerMessageProcessor() 
        {
            _peerManagerInstance = Root.ServiceBusInstance.GetLocalService<IPeerManager>();
        }

        protected async override Task<object> InternalProcessMessageAsync(ServiceBusMessage message)
        {
            object rv = null;

            var deserializedObject = Root.ServiceBusInstance.GetLocalService<ISerializer>().Deserialize(message.Data);

            if (deserializedObject is PeerInfo)
            {
                PeerInfo peer = (PeerInfo)deserializedObject;
                _peerManagerInstance.Notify(peer);
                rv = peer;
            }

            return rv;
        }
    }
}
