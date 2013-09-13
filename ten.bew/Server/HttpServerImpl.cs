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
        public const int TRACEEVENT_ERROR = 0;
        public const int TRACEEVENT_RECEIVED = 1;

        private readonly byte[] _404 = System.Text.Encoding.UTF8.GetBytes("404 not found");

        private byte[] _data;
        private long _requestCount;  
        private long _requestFailed;
        private long _requestCompleteCount;
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
        private TraceSource _tracingHttpServerSource = new TraceSource("httpServer");

        public ICache Cache
        {
            get
            {
                return _cache.Value;
            }
        }
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder(GetType().Name);
            builder.AppendLine();

            builder.Append("\tMaxActive: ");
            builder.AppendLine(Interlocked.Read(ref _maxActive).ToString());
            builder.Append("\tActive: ");
            builder.AppendLine(Interlocked.Read(ref _activeCount).ToString());

            var requestCount = RequestCount;
            var requestCompleteCount = RequestCompleteCount;
            var requestFailed = RequestFailed;

            var tickCount = Interlocked.Read(ref _tickCount);

            builder.Append("\tRequestCount: ");
            builder.AppendLine(RequestCount.ToString());

            builder.Append("\tRequestFailed: ");
            builder.AppendLine(RequestFailed.ToString());

            builder.Append("\tRequestComplete: ");
            builder.AppendLine(Interlocked.Read(ref requestCompleteCount).ToString());

            if(requestCount > 0)
            {
                TimeSpan timeSpan = TimeSpan.FromTicks(tickCount);
                var avg = timeSpan.TotalMilliseconds / (double)requestCount;

                builder.Append("\tAvgTimeToLastByte: ");
                builder.AppendLine(avg.ToString("0.0000000"));
            }

            builder.AppendLine("\tCache:");

            foreach(var item in Cache.Keys)
            {
                builder.Append("\t\t");
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

        public long RequestCount
        {
            get
            {
                return Interlocked.Read(ref _requestCount);
            }
        }

        public long RequestCompleteCount
        {
            get
            {
                return Interlocked.Read(ref _requestCompleteCount);
            }
        }


        public long RequestFailed
        {
            get
            {
                return Interlocked.Read(ref _requestFailed);
            }
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

                _tracingHttpServerSource.TraceInformation("Http Server Started");

                while (_running)
                {
                    try
                    {
                        var context = await _listener.GetContextAsync();
                        Incoming(context);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref _requestFailed);
                        _tracingHttpServerSource.TraceData(TraceEventType.Error, TRACEEVENT_ERROR, ex);
                    }
                }
            }
            catch(Exception ex)
            {
                _tracingHttpServerSource.TraceData(TraceEventType.Error, TRACEEVENT_ERROR, ex);
            }
        }

        private async Task Incoming(HttpListenerContext context)
        {
            try            
            {
                _tracingHttpServerSource.TraceEvent(TraceEventType.Verbose, TRACEEVENT_RECEIVED);
                Interlocked.Increment(ref _requestCount);

                HttpClientImpl client = new HttpClientImpl(context)
                {
                    StartTicks = _timer.ElapsedTicks
                };

                var currentActive = Interlocked.Increment(ref _activeCount);
                var maxActive = Interlocked.Read(ref _maxActive);
                maxActive = Math.Max(currentActive, maxActive);
                Interlocked.Exchange(ref _maxActive, maxActive); // it is possible to overwrite a higher value on a different thread but it's good enough

                byte[] buffer = new byte[1024];
                int read = 0;

                ChunkBase root = null;

                if(Path.GetExtension(context.Request.RawUrl) == "")
                {
                    client.Context.Response.ContentType = "application/json";
                    root = new RESTChunk(this);
                }
                else if (context.Request.RawUrl.EndsWith(".page"))
                {  
                    client.Context.Response.ContentType = "text/html";
                    root = new PageChunk(this, null);
                }
                else
                {
                    client.Context.Response.ContentType = "application/octet";
                    root = new FileChunk(this);
                }

                await root.Send(client, null);

                client.StopTicks = _timer.ElapsedTicks;

                Interlocked.Add(ref _tickCount, client.TimetoLastByte);
            }
            catch(Exception ex)
            {
                _tracingHttpServerSource.TraceData(TraceEventType.Error, TRACEEVENT_ERROR, ex);
            }
            finally
            {
                Interlocked.Increment(ref _requestCompleteCount);
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
