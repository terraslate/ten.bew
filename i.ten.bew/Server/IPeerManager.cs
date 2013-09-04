using i.ten.bew.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace i.ten.bew.Server
{
    public interface IPeerManager
    {
        void Notify(PeerInfo peer);

        IEnumerable<PeerInfo> Peers{ get; }

        void Refresh();

        PeerInfo FindRandomPeerForAddress(string address);
    }
}
