using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ten.bew.Server.Chunks
{
    public abstract class ChunkCreator
    {
        abstract protected internal ChunkBase Create(IServer server, dynamic parameters);
    }
}
