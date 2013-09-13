using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Configuration
{
    public class MainConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("messageProcessors")]
        public KeyValueConfigurationCollection MessageProcessors
        {
            get
            {
                var rv = (KeyValueConfigurationCollection)this["messageProcessors"];
                return rv;
            }
            set
            {
                this["messageProcessors"] = value;
            }
        }

        [ConfigurationProperty("caching")]
        public CachingConfigurationElement Caching
        {
            get
            {
                var rv = (CachingConfigurationElement)this["caching"];
                return rv;
            }
            set
            {
                this["caching"] = value;
            }
        }

        [ConfigurationProperty("serviceBus")]
        public ServiceBusConfigurationElement ServiceBus
        {
            get
            {
                var rv = (ServiceBusConfigurationElement)this["serviceBus"];
                return rv;
            }
            set
            {
                this["serviceBus"] = value;
            }
        }

        [ConfigurationProperty("httpServer")]
        public HttpServerConfigurationElement HttpServer
        {
            get
            {
                var rv = (HttpServerConfigurationElement)this["httpServer"];
                return rv;
            }
            set
            {
                this["httpServer"] = value;
            }
        }
    }

    public class ServiceBusConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("multicast_mac")]
        public string MulticastMAC
        {
            get
            {
                var rv = (string)this["multicast_mac"];
                return rv;
            }
            set
            {
                this["multicast_mac"] = value;
            }
        }

        [ConfigurationProperty("multicast_ip")]
        public string MulticastIP
        {
            get
            {
                var rv = (string)this["multicast_ip"];
                return rv;
            }
            set
            {
                this["multicast_ip"] = value;
            }
        }

        [ConfigurationProperty("multicast_port")]
        public ushort MulticastPort
        {
            get
            {
                var rv = (ushort)this["multicast_port"];
                return rv;
            }
            set
            {
                this["multicast_port"] = value;
            }
        }
    }


    public class CachingConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("shardStart")]
        public ulong ShardStart
        {
            get
            {
                var rv = (ulong)this["shardStart"];
                return rv;
            }
            set
            {
                this["shardStart"] = value;
            }
        }


        [ConfigurationProperty("shardEnd")]
        public ulong ShardEnd
        {
            get
            {
                var rv = (ulong)this["shardEnd"];
                return rv;
            }
            set
            {
                this["shardEnd"] = value;
            }
        }
    }

    public class HttpServerConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("rootDisk")]
        public string RootDisk
        {
            get
            {
                var rv = (string)this["rootDisk"];
                return rv;
            }
            set
            {
                this["rootDisk"] = value;
            }
        }

        [ConfigurationProperty("port")]
        public ushort Port
        {
            get
            {
                var rv = (ushort)this["port"];
                return rv;
            }
            set
            {
                this["port"] = value;
            }
        }
    }
}
