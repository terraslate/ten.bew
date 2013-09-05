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
