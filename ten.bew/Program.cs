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

namespace ten.bew
{
    class Program
    {
        public const string PEER_MANAGER_MESSAGE_PROCESSOR_KEY = "__PeerManagerMessageProcessor";
        public const string CACHING_MESSAGE_PROCESSOR_KEY = "caching";

        private static bool _running = true;
        private static MainConfigurationSection _config;

        public static MainConfigurationSection Config
        {
            get
            {
                return _config;
            }
        }

        static async void BusStarted()
        {
            Console.WriteLine("Started Bus");
        }

        static void BusStopped()
        {
            Console.WriteLine("Stopped Bus");
        }

        static void Main(string[] args)
        {
            Run();
        }

        private static void Run()
        {            
             _config = (MainConfigurationSection)System.Configuration.ConfigurationManager.GetSection("tenbew");

            ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount);
            ThreadPool.SetMaxThreads(Environment.ProcessorCount * 4, Environment.ProcessorCount * 2);

            ServicePointManager.DefaultConnectionLimit = 1000000;
            ServicePointManager.MaxServicePoints = 1000000;

            Root.ServiceBusInstance = new ServiceBusImpl();

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
                            localServerIp.Add(string.Format("http://{0}:801/", ipAddress));
                        }
                    }
                }
            }

            localServerIp.Add(string.Format("http://{0}:801/", "localhost"));

            foreach (var address in localServerIp)
            {
                Console.WriteLine(address);
            }

            var statistics = new ServerStatistics();

            string httpServerRoot = _config.RootDisk;

            if(httpServerRoot.StartsWith("\\"))
            {
                var uri = new Uri(Assembly.GetExecutingAssembly().CodeBase);
                var relativeDir = Path.GetDirectoryName(uri.AbsolutePath);
                relativeDir = relativeDir.Replace("%20", " ");

                httpServerRoot = relativeDir + httpServerRoot;
            }

            if(Directory.Exists(httpServerRoot) == false)
            {
                httpServerRoot = null; // which is fine because we might have a directoryless server.
            }

            IServer server = new HttpServerImpl(httpServerRoot, localServerIp.ToArray());
            server.StartAsync();

            int loopCount = 0;

            while (_running)
            {
                if (loopCount % 10 == 0)
                {
                    SendKeepAlive(serializerService, Program.PEER_MANAGER_MESSAGE_PROCESSOR_KEY);
                    peerManager.Refresh();
                }

                //Console.WriteLine(server);
                //Console.WriteLine();

                Thread.Sleep(1000);

                loopCount++;
            }

            server.Stop();
        }

        private static void ProcessCallback(ServiceBusMessage message, object data)
        {
            Console.WriteLine("ProcessCallback: {0}", data);
        }

        private static void SendKeepAlive(ISerializer serializerService, string pingAddress)
        {
            ServiceBusMessage message = new ServiceBusMessage(pingAddress, serializerService.Serialize(PeerInfo.Self()), DataFormatEnum.BinaryNet);
            Root.ServiceBusInstance.SendAsync(message);
        }
    }
}
