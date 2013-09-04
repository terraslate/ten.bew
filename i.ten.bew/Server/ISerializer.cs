using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace i.ten.bew.Server
{
    public interface ISerializer
    {
        byte[] Serialize(object graph);

        object Deserialize(byte[] serialized);

        T DeserializeAs<T>(byte[] serialized);
    }
}
