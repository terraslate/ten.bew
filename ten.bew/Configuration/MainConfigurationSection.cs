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
}
