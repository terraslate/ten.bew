using i.ten.bew;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace i.ten.bew.Messaging
{
    [Serializable]
    public sealed class PeerInfo
    {
        public static PeerInfo Self()
        {
            PeerInfo rv = new PeerInfo();

            rv.Name = Environment.MachineName;
            rv.PeerTimeUTC = DateTime.UtcNow;
            rv.AddressMap = (from m in Root.ServiceBusInstance.MessageProcessors select m).ToArray();
            rv.IPAddresses = (from n in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                              let macAddress = BitConverter.ToString(n.GetPhysicalAddress().GetAddressBytes())                              
                              where macAddress == Root.ServiceBusInstance.MacAddress && n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up 
                              select n).SelectMany(m => m.GetIPProperties().UnicastAddresses).Where(n => (n.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && System.Net.IPAddress.Loopback.Equals(n.Address) == false)).Select(n => n.Address.ToString()).ToArray();

            return rv;
        }

        private string[] _services;
        private DateTime _lastPing;
        private DateTime _peerTimeUTC;
        private string _name;
        private string[] _addressMap;
        private string[] _IPAddresses;

        public string Name
        {
            get
            {
                return _name;
            }
            private set
            {
                _name = value;
            }
        }

        public string[] IPAddresses
        {
            get
            {
                return _IPAddresses;
            }
            private set
            {
                _IPAddresses = value;
            }
        }

        public string[] AddressMap
        {
            get
            {
                return _addressMap;
            }
            private set
            {
                _addressMap = value;
            }
        }

        public string[] Services
        {
            get
            {
                return _services;
            }
            private set
            {
                _services = value;
            }
        }

        public DateTime PeerTimeUTC 
        {
            get
            {
                return _peerTimeUTC;
            }
            private set
            {
                _peerTimeUTC = value;
            }
        }
    }
}
