using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using System.IO;
using System.Diagnostics;
using i.ten.bew.Server;
using ten.bew.Server.Chunks;

namespace ten.bew.Server
{
    class HttpServerImpl : IServer
    {
        private readonly byte[] _404 = System.Text.Encoding.UTF8.GetBytes("404 not found");

        private byte[] _data;
        private long _requestCount;
        private long _closeRequestCount;
        private long _activeCount;
        private long _tickCount;
        private long _maxActive;
        private bool _listen = false;
        private string[] _prefixes;
        private bool _running;
        private HttpListener _listener;
        private Stopwatch _timer = Stopwatch.StartNew();
        private string _rootDisk;
        private Lazy<ICache> _cache;

        public ICache Cache
        {
            get
            {
                return _cache.Value;
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("MaxActive: ");
            builder.AppendLine(Interlocked.Read(ref _maxActive).ToString());
            builder.Append("Active: ");
            builder.AppendLine(Interlocked.Read(ref _activeCount).ToString());

            var requestCount = Interlocked.Read(ref _requestCount);
            var closeRequestCount = Interlocked.Read(ref _closeRequestCount);
            var tickCount = Interlocked.Read(ref _tickCount);

            builder.Append("RequestCount: ");
            builder.AppendLine(requestCount.ToString());

            builder.Append("CloseRequestCount: ");
            builder.AppendLine(closeRequestCount.ToString());

            if(requestCount > 0)
            {
                TimeSpan timeSpan = TimeSpan.FromTicks(tickCount);
                var avg = timeSpan.TotalMilliseconds / (double)requestCount;

                builder.Append("AvgTimeToLastByte: ");
                builder.AppendLine(avg.ToString("0.0000000"));
            }

            builder.AppendLine("Cache:");

            foreach(var item in Cache.Keys)
            {
                builder.Append("\t");
                builder.Append(item);
                builder.Append("=");
                builder.AppendLine(Cache[item].TokenSource.IsCancellationRequested ? "complete" : "building");
            }

            return builder.ToString();
        }

        public HttpServerImpl( string rootDisk, params string[] prefixes)
        {
            _cache = new Lazy<ICache>(() => { return new CacheImpl(); },  LazyThreadSafetyMode.ExecutionAndPublication);
            _rootDisk = rootDisk;

            if(_rootDisk.EndsWith("\\"))
            {
                _rootDisk = _rootDisk.Substring(0, _rootDisk.Length - 1);
            }

            _prefixes = (from p in prefixes orderby prefixes.Length descending select p).ToArray();
        }


        public async Task StartAsync()
        {
            try
            {
                _running = true;
                _listener = new HttpListener();

                foreach (var prefix in _prefixes)
                {
                    _listener.Prefixes.Add(prefix);
                }

                _listener.Start();

                while (_running)
                {
                    try
                    {
                        var context = await _listener.GetContextAsync();

                        HttpClientImpl client = new HttpClientImpl(context)
                        {
                            StartTicks = _timer.ElapsedTicks
                        };

                        Interlocked.Increment(ref _requestCount);
                        var task = Incoming(client);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async Task Incoming(HttpClientImpl client)
        {
            try            
            {
                HttpListenerContext context = client.Context;
                var currentActive = Interlocked.Increment(ref _activeCount);
                var maxActive = Interlocked.Read(ref _maxActive);
                maxActive = Math.Max(currentActive, maxActive);
                Interlocked.Exchange(ref _maxActive, maxActive); // it is possible to overwrite a higher value on a different thread but it's good enough

                byte[] buffer = new byte[1024];
                int read = 0;

                ChunkBase root = null;

                if(Path.GetExtension(context.Request.RawUrl) == "")
                {
                    root = new RESTChunk(this);
                }
                else if (context.Request.RawUrl.EndsWith(".page"))
                {
                    root = new PageChunk(this, null);
                }
                else
                {
                    root = new FileChunk(this);
                }

                await root.Send(client, null);

                client.StopTicks = _timer.ElapsedTicks;

                Interlocked.Add(ref _tickCount, client.TimetoLastByte);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Interlocked.Increment(ref _closeRequestCount);
                Interlocked.Decrement(ref _activeCount);   
            }
        }

        public byte[] GetStatusCode(int code)
        {
            switch(code)
            {
                case 404:
                    return _404;

                default:
                    return _404;
            }
        }

        public string GetFileName(string fromUrl)
        {
            string rv = null;

            if(fromUrl.EndsWith("/"))
            {
                rv = _rootDisk + (fromUrl + "index.html").Replace("/","\\");
            }
            else
            {
                rv = _rootDisk + (fromUrl).Replace("/", "\\");
            }

            return rv;
        }


        public void Stop()
        {
            _listener.Stop();
            _listener.Close();
            _listener = null;
        }
    }
}
