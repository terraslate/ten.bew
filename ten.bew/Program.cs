using ten.bew.Server;
using i.ten.bew.Messaging;
using i.ten.bew.Server;
using i.ten.bew;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ten.bew.Messaging;
using ten.bew.Caching;
using ten.bew.Configuration;
using System.Reflection;
using System.IO;
using System.Diagnostics;

namespace ten.bew
{
    class Program
    {
        private static LoopAndPumpScheduler LPScheduler;

        public static readonly TraceSource HealthTracing = new TraceSource("health");

        public const string PEER_MANAGER_MESSAGE_PROCESSOR_KEY = "__PeerManagerMessageProcessor";
        public const string CACHING_MESSAGE_PROCESSOR_KEY = "caching";

        private static bool _running = true;
        private static MainConfigurationSection _config;
        public static readonly Stopwatch Watch = Stopwatch.StartNew();

        public static MainConfigurationSection Config
        {
            get
            {
                return _config;
            }
        }

        static async void BusStarted()
        {
            HealthTracing.TraceInformation("Started Bus");
        }

        static void BusStopped()
        {
            HealthTracing.TraceInformation("Stopped Bus");
        }

        static void Main(string[] args)
        {
            _config = (MainConfigurationSection)System.Configuration.ConfigurationManager.GetSection("tenbew");

            //LPScheduler = new LoopAndPumpScheduler();
            //CancellationTokenSource globalCancelSource = new CancellationTokenSource();
            //TaskFactory factory = new TaskFactory(globalCancelSource.Token, TaskCreationOptions.None, TaskContinuationOptions.None, LPScheduler);
            //factory.StartNew(Run);

            Run();
        }

        private static void Run()
        {
            ServicePointManager.DefaultConnectionLimit = 1000000;
            ServicePointManager.MaxServicePoints = 1000000;

            Root.ServiceBusInstance = new ServiceBusImpl(Config.ServiceBus.MulticastMAC, Config.ServiceBus.MulticastIP, Config.ServiceBus.MulticastPort);

            ISerializer serializerService = Root.ServiceBusInstance.AddLocalService<ISerializer>(new Serializer());
            IPeerManager peerManager = Root.ServiceBusInstance.AddLocalService<IPeerManager>(new PeerManagerImpl());

            foreach (var key in Config.MessageProcessors.AllKeys)
            {
                var element = Config.MessageProcessors[key];

                string typeName = element.Value;
                Type type = Type.GetType(typeName);

                if (type != null)
                {
                    var wrapped = Activator.CreateInstance(type);
                    IMessageProcessor processor = (IMessageProcessor)wrapped;
                    Root.ServiceBusInstance.AddMessageProcessor(key, processor);
                }
            }

            IMessageProcessor peerManagerMP = Root.ServiceBusInstance.GetMessageProcessor(Program.PEER_MANAGER_MESSAGE_PROCESSOR_KEY);
            IMessageProcessor cachingMP = Root.ServiceBusInstance.GetMessageProcessor(Program.CACHING_MESSAGE_PROCESSOR_KEY);

            Root.ServiceBusInstance.StartAsync(BusStarted, BusStopped);

            List<string> localServerIp = new List<string>();

            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet && nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                {
                    foreach (var address in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            var ipAddress = address.Address.ToString();
                            localServerIp.Add(string.Format("http://{0}:{1}/", ipAddress, _config.HttpServer.Port));
                        }
                    }
                }
            }

            localServerIp.Add(string.Format("http://{0}:{1}/", "localhost", _config.HttpServer.Port));

            foreach (var address in localServerIp)
            {
                HealthTracing.TraceInformation(address);
            }

            var statistics = new ServerStatistics();

            string httpServerRoot = _config.HttpServer.RootDisk;

            if (httpServerRoot.StartsWith("\\"))
            {
                var uri = new Uri(Assembly.GetExecutingAssembly().CodeBase);
                var relativeDir = Path.GetDirectoryName(uri.AbsolutePath);
                relativeDir = WebUtility.UrlDecode(relativeDir);

                httpServerRoot = relativeDir + httpServerRoot;
            }

            if (Directory.Exists(httpServerRoot) == false)
            {
                httpServerRoot = null; // which is fine because we might have a directoryless server.
            }

            IServer httpServer = new HttpServerImpl(httpServerRoot, localServerIp.ToArray());
            httpServer.StartAsync();

            int loopCount = 0;

            ServiceBusImpl typedSB = (ServiceBusImpl)Root.ServiceBusInstance;

            while (_running)
            {
                SendKeepAlive(serializerService, Program.PEER_MANAGER_MESSAGE_PROCESSOR_KEY);
                peerManager.Refresh();

                //HealthTracing.TraceInformation("Tasks:{0}{1}", Environment.NewLine, LPScheduler.Count);
                HealthTracing.TraceInformation("ServiceBus:{0}{1}", Environment.NewLine, Root.ServiceBusInstance.ToString());
                HealthTracing.TraceInformation(httpServer.ToString());

                foreach (var key in Root.ServiceBusInstance.MessageProcessors)
                {
                    try
                    {
                        HealthTracing.TraceInformation("Key={1}{0}{2}", Environment.NewLine, key, Root.ServiceBusInstance.GetMessageProcessor(key).ToString());
                    }
                    catch (Exception ex)
                    {
                    }
                }

                Thread.Sleep(5000);

                loopCount++;
            }

            httpServer.Stop();
        }

        private static void SendKeepAlive(ISerializer serializerService, string pingAddress)
        {
            ServiceBusMessage message = new ServiceBusMessage(pingAddress, serializerService.Serialize(PeerInfo.Self()), DataFormatEnum.BinaryNet);
            Root.ServiceBusInstance.SendAsync(message);
        }
    }
}
