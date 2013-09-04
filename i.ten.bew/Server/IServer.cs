using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace i.ten.bew.Server
{
    public interface IServer
    {
        Task StartAsync();

        void Stop();

        ICache Cache { get; }

        string GetFileName(string fromUrl);

        byte[] GetStatusCode(int code);
    }
}
