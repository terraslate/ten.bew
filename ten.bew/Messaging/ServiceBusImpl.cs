using i.ten.bew;
using i.ten.bew.Messaging;
using i.ten.bew.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public const int TRACEEVENT_ERROR = 0;
        public const int TRACEEVENT_DISCARDMESSAGE = 1;
        public const int TRACEEVENT_RECEIVEMESSAGE = 2;
        public const int TRACEEVENT_SENDASYNC = 3;

        private static readonly Stopwatch _watch = Stopwatch.StartNew();

        private Dictionary<string, IMessageProcessor> _messageProcessors;
        private Dictionary<Type, Object> _localServices;
        private UdpClient _receiverClient;
        private UdpClient _senderClient;
        private bool _listening;
        private string _multicastMAC;
        private string _multicastIP;
        private int _multicastPort;
        private LinkedList<ReplyTaskDictionary> _outgoingMessages;
        private TraceSource _serviceBusTracing = new TraceSource("serviceBus");
        private long _receivedDataGram, _acceptDataGram, _rejectDataGram, _acceptMessage, _rejectMessage, _replyMessage, _sendingAsync, _sentAsyncOK, _sentAsyncFailed, _replyMessageNotFound;
        private System.Reflection.PropertyInfo[] _properties;
        private int _sendAsyncReplyCount;

        public ServiceBusImpl(string multicastMac, string multicastIP, ushort multicastPort)
        {
            _properties = GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy);
            _multicastMAC = multicastMac;
            _multicastIP = multicastIP;
            _multicastPort = multicastPort;
            _outgoingMessages = new LinkedList<ReplyTaskDictionary>();
            _localServices = new Dictionary<Type, object>();
            _messageProcessors = new Dictionary<string, IMessageProcessor>();

            if (Int32.TryParse(ConfigurationManager.AppSettings["multicastPort"], out _multicastPort) == false)
            {
                _multicastPort = 4567;
            }
        }

        public long ReceivedDataGram
        {
            get
            {
                return Interlocked.Read(ref _receivedDataGram);
            }
        }

        public long AcceptDataGram
        {
            get
            {
                return Interlocked.Read(ref _acceptDataGram);
            }
        }

        public long RejectDataGram
        {
            get
            {
                return Interlocked.Read(ref _rejectDataGram);
            }
        }


        public long AcceptMessage
        {
            get
            {
                return Interlocked.Read(ref _acceptMessage);
            }
        }

        public long SendingAsync
        {
            get
            {
                return Interlocked.Read(ref _sendingAsync);
            }
        }

        public long SentAsyncOK
        {
            get
            {
                return Interlocked.Read(ref _sentAsyncOK);
            }
        }

        public long SentAsyncFailed
        {
            get
            {
                return Interlocked.Read(ref _sentAsyncFailed);
            }
        }

        public long RejectMessage
        {
            get
            {
                return Interlocked.Read(ref _rejectMessage);
            }
        }
        public long ReplyMessageNotFound
        {
            get
            {
                return Interlocked.Read(ref _replyMessageNotFound);
            }
        }

        public string MulticastAddress
        {
            get
            {
                return _multicastIP;
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
                return _multicastPort;
            }
        }

        public string MacAddress
        {
            get
            {
                return _multicastMAC;
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

                _serviceBusTracing.TraceInformation(netInterface.Name + ": " + macAddress);

                if (_multicastMAC == macAddress)
                {
                    foundMac = true;

                    var mca = netInterface.GetIPProperties().MulticastAddresses;

                    _serviceBusTracing.TraceInformation(" - Multicast address count {0}", (mca == null ? 0 : mca.Count));

                    foreach (var multicastAddress in mca)
                    {
                        if (multicastAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            _serviceBusTracing.TraceInformation(" Found ip {0}.", multicastAddress.Address);

                            if ((multicastAddress.Address.ToString() == _multicastIP))
                            {
                                foundIp = true;

                                _receiverClient = new UdpClient(_multicastPort);
                                _receiverClient.MulticastLoopback = false;

                                _senderClient = new UdpClient(_multicastIP, _multicastPort);

                                _serviceBusTracing.TraceInformation("\t");
                                _serviceBusTracing.TraceInformation(_multicastIP);

                                try
                                {
                                    _receiverClient.JoinMulticastGroup(multicastAddress.Address);
                                    _serviceBusTracing.TraceInformation(" joined.");
                                }
                                catch (Exception ex)
                                {
                                    _serviceBusTracing.TraceInformation(" not joined. ");
                                    _serviceBusTracing.TraceData(TraceEventType.Warning, 0, ex);
                                }

                                break;
                            }
                        }
                    }

                    break;
                }
                else
                {
                    _serviceBusTracing.TraceInformation(" ignored.");
                }
            }

            if (foundMac == false || foundIp == false)
            {
                Stop();
                var error = new InvalidOperationException("Can't listen on the specified natwork interface card.");
                _serviceBusTracing.TraceData(TraceEventType.Critical, 0, error);
                throw error;
            }

            if (startedHandler != null)
            {
                startedHandler();
            }

            Cleanup();

            int cleanupInterval = (int)TimeSpan.FromSeconds(12).TotalMilliseconds;

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

            if (removed != null)
            {
                try
                {
                    removed.Value.Cancel();
                }
                catch (Exception ex)
                {
                    _serviceBusTracing.TraceData(TraceEventType.Warning, TRACEEVENT_ERROR, ex);
                }
            }
        }

        private Dictionary<string, Reference<int>> _messageCounter = new Dictionary<string, Reference<int>>();

        private async Task ListenLoop()
        {
            while (_listening)
            {
                try
                {
                    var result = await _receiverClient.ReceiveAsync();
                    Interlocked.Increment(ref _receivedDataGram);
                    ProcessDataGram(result);
                }
                catch (Exception ex)
                {
                    _serviceBusTracing.TraceData(TraceEventType.Error, TRACEEVENT_ERROR, ex);
                }
            }
        }

        private async Task ProcessDataGram(UdpReceiveResult result)
        {
            try
            {
                ServiceBusMessage message = null;
                BinaryFormatter formatter = new BinaryFormatter();

                using (MemoryStream ms = new MemoryStream(result.Buffer))
                {
                    message = formatter.Deserialize(ms) as ServiceBusMessage;
                }

                if (message == null)
                {
                    Interlocked.Increment(ref _rejectDataGram);
                    _serviceBusTracing.TraceEvent(TraceEventType.Verbose, TRACEEVENT_DISCARDMESSAGE, "Discarded message.");
                    return;
                }
                else
                {
                    Interlocked.Increment(ref _acceptDataGram);
                }

                _serviceBusTracing.TraceData(TraceEventType.Verbose, TRACEEVENT_RECEIVEMESSAGE, message);

                if ((ServiceBusMessage.TO_ALL_NODES.Equals(message.ToNode) || Environment.MachineName.Equals(message.ToNode)) && _messageProcessors.ContainsKey(message.Address))
                {
                    Interlocked.Increment(ref _acceptMessage);

                    var messageProcessor = _messageProcessors[message.Address];

                    Task replyTask = null;
                    IReplyInfo replyReferenceInfo = null;

                    if (Guid.Empty.Equals(message.InReplyTo) == false)
                    {
                        TimeSpan started = _watch.Elapsed;

                        lock (_outgoingMessages)
                        {
                            TimeSpan duration = _watch.Elapsed - started;

                            if (duration.TotalSeconds >= 3)
                            {
                                _serviceBusTracing.TraceInformation("Long lock");
                            }

                            foreach (var value in _outgoingMessages)
                            {
                                if (value.ContainsKey(message.InReplyTo))
                                {
                                    replyTask = value[message.InReplyTo];
                                    replyReferenceInfo = (IReplyInfo)replyTask;

                                    if (replyTask != null)
                                    {
                                        Interlocked.Increment(ref _replyMessage);
                                        value.Remove(message.InReplyTo);
                                        replyReferenceInfo.ReplyMessage = message;
                                        break;
                                    }
                                }
                                else
                                {
                                    _serviceBusTracing.TraceInformation("Trying next message container.");
                                }
                            }
                        }

                        if (_replyMessage == null)
                        {
                            Interlocked.Increment(ref _replyMessageNotFound);
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
                else
                {
                    Interlocked.Increment(ref _rejectMessage);
                }
            }
            catch (Exception ex)
            {
                _serviceBusTracing.TraceData(TraceEventType.Error, TRACEEVENT_ERROR, ex);
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
                _serviceBusTracing.TraceData(TraceEventType.Warning, TRACEEVENT_ERROR, ex);
            }
            finally
            {
                _listening = false;
            }
        }
        public long SendAsyncReplyCount
        {
            get
            {
                return _sendAsyncReplyCount;
            }
        }

        public async Task SendAsync<P>(ServiceBusMessage message, Reference<ReplyTask<P>> replyReference) where P : class
        {
            UdpClient udpSender = null;

            if (ServiceBusMessage.TO_ALL_NODES.Equals(message.ToNode))
            {
                udpSender = _senderClient;
            }
            else
            {
                var peerInfo = GetLocalService<IPeerManager>().Peers.Where(s => s.Name == message.ToNode).FirstOrDefault();

                if (peerInfo != null && peerInfo.IPAddresses.Length > 0)
                {
                    Interlocked.Increment(ref _sendAsyncReplyCount);

                    var peerAddress = peerInfo.IPAddresses[0];
                    var senderClient = new UdpClient(peerAddress, _multicastPort);
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
                        message.MarkOriginatorRequiresReply();
                    }
                }

                Interlocked.Increment(ref _sendingAsync);

                message.SetSendTimeUTC(DateTime.UtcNow);
                var data = Root.ServiceBusInstance.GetLocalService<ISerializer>().Serialize(message);

                udpSender.SendAsync(data, data.Length).ContinueWith(
                    (task) =>
                    {
                        if (task.Status != TaskStatus.RanToCompletion)
                        {
                            _serviceBusTracing.TraceEvent(TraceEventType.Warning, TRACEEVENT_SENDASYNC, "SendAsync {1} - {0} bytes.", task.Result, task.Status);
                            Interlocked.Increment(ref _sentAsyncFailed);
                        }
                        else
                        {
                            _serviceBusTracing.TraceEvent(TraceEventType.Verbose, TRACEEVENT_SENDASYNC, "SendAsync OK {0} bytes.", task.Result);
                            Interlocked.Increment(ref _sentAsyncOK);
                        }

                        _serviceBusTracing.TraceEvent(TraceEventType.Verbose, TRACEEVENT_SENDASYNC, "SendAsync {0} bytes.", task.Result);
                    }
                );

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

            if (_messageProcessors.ContainsKey(address))
            {
                rv = _messageProcessors[address];
            }

            return rv;
        }

        public int AwaitingReply
        {
            get
            {
                int rv = 0;

                lock (_outgoingMessages)
                {
                    foreach (var item in _outgoingMessages)
                    {
                        rv += item.Count;
                    }
                }

                return rv;
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder(GetType().Name);
            builder.AppendLine();

            foreach (var property in _properties)
            {
                builder.Append("\t");
                builder.Append(property.Name);
                builder.Append(": ");

                string sValue = "{NULL}";
                object value = property.GetValue(this);

                if (property.PropertyType != typeof(string))
                {
                    var converter = TypeDescriptor.GetConverter(property.PropertyType);

                    if (converter != null)
                    {
                        sValue = converter.ConvertToString(value);
                    }
                }
                else
                {
                    if (value != null)
                    {
                        sValue = (string)value;
                    }
                }

                builder.AppendLine(sValue);
            }

            return builder.ToString();
        }
    }
}
