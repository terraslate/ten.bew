using i.ten.bew;
using i.ten.bew.Messaging;
using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ten.bew.Messaging
{
    class ServiceBusImpl : IServiceBus
    {
        private static List<Task> replies = new List<Task>();

        private Dictionary<string, IMessageProcessor> _messageProcessors;
        private Dictionary<Type, Object> _localServices;
        private UdpClient _receiverClient;
        private UdpClient _senderClient;
        private bool _listening;
        private string _macAddress = ConfigurationManager.AppSettings["multicastMAC"];
        private string _multicastAddress = ConfigurationManager.AppSettings["multicastIP"];
        private int _port = Int32.Parse(ConfigurationManager.AppSettings["multicastPort"]);
        private LinkedList<ReplyTaskDictionary> _outgoingMessages;

        public ServiceBusImpl()
        {
            _outgoingMessages = new LinkedList<ReplyTaskDictionary>();
            _localServices = new Dictionary<Type, object>();
            _messageProcessors = new Dictionary<string, IMessageProcessor>();
            _macAddress = ConfigurationManager.AppSettings["multicastMAC"];
            _multicastAddress = ConfigurationManager.AppSettings["multicastIP"];

            if (Int32.TryParse(ConfigurationManager.AppSettings["multicastPort"], out _port) == false)
            {
                _port = 4567;
            }
        }

        public string MulticastAddress
        {
            get
            {
                return _multicastAddress;
            }
        }

        public IEnumerable<string> MessageProcessors
        {
            get
            {
                foreach (var mp in _messageProcessors)
                {
                    yield return mp.Key;
                }
            }
        }

        public int Port
        {
            get
            {
                return _port;
            }
        }

        public string MacAddress
        {
            get
            {
                return _macAddress;
            }
        }


        public async Task StartAsync(Action startedHandler, Action stoppedHandler)
        {
            _listening = true;

            bool foundMac = false;
            bool foundIp = false;
            var netInterfaces = (from netInterface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces() where netInterface.SupportsMulticast && netInterface.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up select netInterface);

            foreach (var netInterface in netInterfaces)
            {
                var properties = netInterface.GetIPProperties();

                string macAddress = BitConverter.ToString(netInterface.GetPhysicalAddress().GetAddressBytes());

                Console.Write(netInterface.Name + ": " + macAddress);

                if (_macAddress == macAddress)
                {
                    foundMac = true;

                    var mca = netInterface.GetIPProperties().MulticastAddresses;

                    Console.WriteLine(" - Multicast address count {0}", (mca == null ? 0 : mca.Count));

                    foreach (var multicastAddress in mca)
                    {
                        if (multicastAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            Console.WriteLine(" Found ip {0}.", multicastAddress.Address);

                            if ((multicastAddress.Address.ToString() == _multicastAddress))
                            {
                                foundIp = true;

                                _receiverClient = new UdpClient(_port);
                                _receiverClient.MulticastLoopback = false;

                                _senderClient = new UdpClient(_multicastAddress, _port);

                                Console.Write("\t");
                                Console.Write(_multicastAddress);

                                try
                                {
                                    _receiverClient.JoinMulticastGroup(multicastAddress.Address);
                                    Console.WriteLine(" joined.");
                                }
                                catch (Exception ex)
                                {
                                    Console.Write(" not joined. ");
                                    Console.WriteLine(ex.Message);
                                }

                                break;
                            }
                        }
                    }

                    break;
                }
                else
                {
                    Console.WriteLine(" ignored.");
                }
            }

            if (foundMac == false || foundIp == false)
            {
                Stop();
                throw new InvalidOperationException("Can't listen on the specified natwork interface card.");
            }

            if (startedHandler != null)
            {
                startedHandler();
            }

            Cleanup();

            int cleanupInterval = (int)TimeSpan.FromSeconds(60000).TotalMilliseconds;

            using (Timer timer = new Timer((o) => { Cleanup(); }, null, cleanupInterval, cleanupInterval))
            {
                await ListenLoop();
            }

            if (stoppedHandler != null)
            {
                stoppedHandler();
            }
        }

        private void Cleanup()
        {
            LinkedListNode<ReplyTaskDictionary> removed = null;

            if (_listening)
            {
                lock (_outgoingMessages)
                {
                    if (_outgoingMessages.Count == 5)
                    {
                        removed = _outgoingMessages.Last;
                        _outgoingMessages.RemoveLast();
                    }

                    _outgoingMessages.AddFirst(new LinkedListNode<ReplyTaskDictionary>(new ReplyTaskDictionary()));
                }
            }

            if(removed != null)            
            {
                try
                {
                    removed.Value.Cancel();
                }
                catch(Exception ex)
                {
                    Debug.WriteLine(string.Format("{0} : {1}", ex.StackTrace, ex.Message));
                }
            }
        }

        private async Task ListenLoop()
        {
            while (_listening)
            {
                try
                {
                    var result = await _receiverClient.ReceiveAsync();

                    ServiceBusMessage message = null;
                    BinaryFormatter formatter = new BinaryFormatter();

                    using (MemoryStream ms = new MemoryStream(result.Buffer))
                    {
                        message = formatter.Deserialize(ms) as ServiceBusMessage;
                    }

                    if(message == null)
                    {
                        Console.WriteLine("Discarded message.");
                        continue;
                    }

                    Console.WriteLine("Received Message: {0}", message);

                    if ((ServiceBusMessage.TO_ALL_NODES.Equals(message.ToNode) || Environment.MachineName.Equals(message.ToNode)) && _messageProcessors.ContainsKey(message.Address))
                    {
                        if (_messageProcessors.ContainsKey(message.Address))
                        {
                            var messageProcessor = _messageProcessors[message.Address];
                            
                            Task replyTask = null;
                            IReplyInfo replyReferenceInfo = null;

                            if (Guid.Empty.Equals(message.InReplyTo) == false)
                            {
                                lock (_outgoingMessages)
                                {
                                    foreach (var value in _outgoingMessages)
                                    {
                                        if (value.ContainsKey(message.InReplyTo))
                                        {
                                            replyTask = value[message.InReplyTo];
                                            replyReferenceInfo = (IReplyInfo)replyTask;

                                            if (replyTask != null)
                                            {
                                                value.Remove(message.InReplyTo);
                                                replyReferenceInfo.ReplyMessage = message;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            var executing = messageProcessor.ProcessMessageAsync(message).ContinueWith(
                                (task) => 
                                {
                                    if (replyTask != null)
                                    {
                                        replyReferenceInfo.Payload = task.Result;
                                        replyTask.Start();
                                    }
                                }
                            );
                        }
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private void Start(Action startedHandler, Action stoppedHandler)
        {
            StartAsync(startedHandler, stoppedHandler).Wait();
        }

        public void Stop()
        {
            try
            {
                lock (_outgoingMessages)
                {
                    _outgoingMessages.Clear();
                }

                _receiverClient.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                _listening = false;
            }
        }

        public async Task SendAsync<P>(ServiceBusMessage message, Reference<ReplyTask<P>> replyReference) where P : class
        {
            UdpClient udpSender = null;

            var data = Root.ServiceBusInstance.GetLocalService<ISerializer>().Serialize(message);

            if (ServiceBusMessage.TO_ALL_NODES.Equals(message.ToNode))
            {
                udpSender = _senderClient;
            }
            else
            {
                var peerInfo = GetLocalService<IPeerManager>().Peers.Where(s => s.Name == message.ToNode).FirstOrDefault();

                if (peerInfo != null && peerInfo.IPAddresses.Length > 0)
                {
                    var peerAddress = peerInfo.IPAddresses[0];
                    var senderClient = new UdpClient(peerAddress, _port);
                    udpSender = senderClient;
                }
                else
                {
                    throw new Exception("There is no known peer, or a known peer has no known ip addresses.");
                }
            }

            if (udpSender != null)
            {
                if (replyReference != null)
                {
                    lock (_outgoingMessages)
                    {
                        replyReference.Item = _outgoingMessages.First.Value.AddForMessage<P>(message);
                    }
                }

                udpSender.SendAsync(data, data.Length).ContinueWith((task) => { Console.WriteLine("SendAsync {0} bytes.", task.Result); });
            }
            else
            {
                throw new Exception("There is no transport available to send this message on.");
            }
        }

        public async Task SendAsync<P>(string address, string message, Reference<ReplyTask<P>> replyReference) where P : class
        {
            ServiceBusMessage messageObject = new ServiceBusMessage(address, System.Text.Encoding.UTF8.GetBytes(message), DataFormatEnum.BinaryNet);
            await SendAsync(messageObject, replyReference);
        }
        public async Task SendAsync<P>(string address, byte[] message, DataFormatEnum dataFormat, Reference<ReplyTask<P>> replyReference) where P : class
        {
            ServiceBusMessage messageObject = new ServiceBusMessage(address, message, dataFormat);
            await SendAsync(messageObject, replyReference);
        }

        public async Task SendAsync(ServiceBusMessage message)
        {
            await SendAsync<object>(message, null);
        }

        public async Task SendAsync(string message, string data)
        {
            await SendAsync<object>(message, data, null);
        }

        public async Task SendAsync(string address, byte[] data, DataFormatEnum dataFormat)
        {
            await SendAsync<object>(address, data, dataFormat, null);
        }


        public IMessageProcessor AddMessageProcessor(string address, IMessageProcessor service)
        {
            if (_messageProcessors.ContainsKey(address) == false)
            {
                _messageProcessors.Add(address, service);
            }

            return _messageProcessors[address];
        }

        public IMessageProcessor RemoveMessageProcessor(string address)
        {
            var rv = _messageProcessors[address];

            _messageProcessors.Remove(address);

            return rv;
        }

        public T AddLocalService<T>(T serviceType)
        {
            _localServices.Add(typeof(T), serviceType);
            return serviceType;
        }

        public object RemoveLocalService(Type type)
        {
            object rv = _localServices[type];

            _localServices.Remove(type);

            return rv;
        }

        public T RemoveLocalService<T>()
        {
            return (T)RemoveLocalService(typeof(T));
        }

        public T GetLocalService<T>()
        {
            return (T)_localServices[typeof(T)];
        }



        public IMessageProcessor GetMessageProcessor(string address)
        {
            IMessageProcessor rv = null;

            if(_messageProcessors.ContainsKey(address))
            {
                rv = _messageProcessors[address];
            }

            return rv;
        }
    }
}
