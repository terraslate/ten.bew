using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace i.ten.bew.Server
{
    public interface IClient<C>
    {
        C Context { get; }
    }
}
