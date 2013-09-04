using i.ten.bew.Messaging;
using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Messaging
{
    class PeerManagerImpl : IPeerManager
    {
        private TimeSpan TTL = TimeSpan.FromMinutes(1); 
        private Dictionary<string, PeerManagerInfo> _peers = new Dictionary<string, PeerManagerInfo>();
        private Random random = new Random();

        private class PeerManagerInfo
        {   
            public readonly PeerInfo Peer;
            public readonly DateTime LastPingUTC;

            public PeerManagerInfo(PeerInfo peer)
            {
                LastPingUTC = DateTime.UtcNow;
                Peer = peer;
            }
        }

        public void Notify(PeerInfo peer)
        {
            lock (_peers)
            {
                _peers[peer.Name] = new PeerManagerInfo(peer);
            }
        }

        public IEnumerable<PeerInfo> Peers
        {
            get
            {
                DateTime utcNow = DateTime.UtcNow;
                TimeSpan activeAgo = TimeSpan.Zero;

                lock (_peers)
                {
                    foreach (var keyValue in _peers)                    
                    {
                        activeAgo = (utcNow - keyValue.Value.LastPingUTC);

                        if (activeAgo < TTL)
                        {
                            yield return keyValue.Value.Peer;
                        }
                    }
                }
            }
        }

        public void Refresh()
        {
            List<string> removeKeys = new List<string>();
            DateTime utcNow = DateTime.UtcNow;
            TimeSpan activeAgo = TimeSpan.Zero;

            lock (_peers)
            {
                foreach (var peer in _peers)
                {
                    activeAgo = (utcNow - peer.Value.LastPingUTC);

                    if (activeAgo > TTL)
                    {
                        removeKeys.Add(peer.Key);
                    }
                }

                foreach (var key in removeKeys)
                {
                    _peers.Remove(key);
                }
            }
        }


        public PeerInfo FindRandomPeerForAddress(string address)
        {
            PeerInfo rv = null;

            PeerInfo[] peers = null;

            lock (_peers)
            {
                peers = (from p in _peers where p.Value.Peer.AddressMap.Contains(address) select p.Value.Peer).ToArray();
            }

            if (peers != null)
            {
                if (peers.Length > 0)
                {
                    int randomIndex = random.Next(0, peers.Length);
                    rv = peers[randomIndex];
                }
            }

            return rv;
        }
    }
}
