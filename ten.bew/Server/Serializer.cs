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
    class Serializer : ISerializer
    {
        public byte[] Serialize(object graph)
        {
            byte[] rv = null;
            BinaryFormatter formatter = new BinaryFormatter();

            using (MemoryStream ms = new MemoryStream())
            {
                formatter.Serialize(ms, graph);
                rv = ms.ToArray();
            }

            return rv;
        }

        public object Deserialize(byte[] serialized)
        {
            object rv = null;
            BinaryFormatter formatter = new BinaryFormatter();

            using (MemoryStream ms = new MemoryStream(serialized))
            {
                rv = formatter.Deserialize(ms);
            }

            return rv;
        }
        public T DeserializeAs<T>(byte[] serialized)
        {
            return (T)Deserialize(serialized);
        }
    }
}
