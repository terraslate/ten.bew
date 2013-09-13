using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace i.ten.bew.Messaging
{
    [Serializable]
    public sealed class ServiceBusMessage
    {
        public const string TO_ALL_NODES = "*";

        private bool _compressed;
        private double _latitude;
        private double _longitude;

        public readonly string ToNode = TO_ALL_NODES;
        public readonly string Originator;
        public readonly Guid MessageId;
        public readonly Guid InReplyTo;
        public readonly string Address;
        public readonly DataFormatEnum DataFormat;

        private bool _originatorRequiresReply;  
        private DateTime _dateTimeUTC;
        private byte[] _data;

        public void MarkOriginatorRequiresReply()
        {
            _originatorRequiresReply = true;
        }

        public bool OriginatorRequiresReply
        {
            get
            {
                return _originatorRequiresReply;
            }
        }

        public double Longitude
        {
            get
            {
                return _longitude;
            }
        }

        public double Latitude
        {
            get
            {
                return _latitude;
            }
        }

        public void SetLocation(double latitude, double longitude)
        {
            _latitude = latitude;
            _longitude = longitude;
        }

        public byte[] Data
        {
            get
            {
                if (_compressed)
                {
                    using (var compressedData = new MemoryStream(_data))
                    using (var decompressedData = new MemoryStream())
                    {
                        using (var cs = new System.IO.Compression.DeflateStream(compressedData, System.IO.Compression.CompressionMode.Decompress))
                        {
                            cs.CopyTo(decompressedData);
                        }

                        _data = decompressedData.ToArray();
                        _compressed = false;
                    }
                }

                return _data;
            }
            private set
            {
                _data = value;
            }
        }

        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            if (_data.Length > 256)
            {
                if (_compressed == false)
                {
                    using (var compressedData = new MemoryStream())
                    {
                        using (var cs = new System.IO.Compression.DeflateStream(compressedData, System.IO.Compression.CompressionLevel.Optimal))
                        {
                            cs.Write(_data, 0, _data.Length);
                        }

                        compressedData.Flush();
                        _data = compressedData.ToArray();
                        _compressed = true;
                    }
                }
            }
        }

        public ServiceBusMessage(string address, byte[] data, DataFormatEnum dataFormat)
            : this(address, data, dataFormat, TO_ALL_NODES, Guid.Empty)
        {
        }

        public ServiceBusMessage(string address, byte[] data, DataFormatEnum dataFormat, string toNode)
            : this(address, data, dataFormat, toNode, Guid.Empty)
        {
        }

        public ServiceBusMessage(string address, byte[] data, DataFormatEnum dataFormat, string toNode, Guid inReplyTo)
        {
            Originator = Environment.MachineName;
            MessageId = Guid.NewGuid();
            Address = address;
            Data = data;
            ToNode = toNode;
            InReplyTo = inReplyTo;
            DataFormat = dataFormat;
        }

        public static ServiceBusMessage CreateReply(ServiceBusMessage message, byte[] data, DataFormatEnum dataFormat)
        {
            return CreateReply(message, message.Address, data, dataFormat);
        }

        public static ServiceBusMessage CreateReply(ServiceBusMessage message, string address, byte[] data, DataFormatEnum dataFormat)
        {
            ServiceBusMessage reply = new ServiceBusMessage(address, data, dataFormat, message.Originator, message.MessageId);
            return reply;
        }

        public override string ToString()
        {
            var rv = JsonConvert.SerializeObject(this);
            return rv;
        }

        public void SetSendTimeUTC(DateTime dateTimeUTC)
        {
            if(dateTimeUTC != DateTime.MinValue)
            {
                _dateTimeUTC = dateTimeUTC;
            }
        }

        public TimeSpan Age
        {
            get
            {
                TimeSpan rv = TimeSpan.MinValue;

                if (_dateTimeUTC > DateTime.MinValue)
                {
                    rv = DateTime.UtcNow - _dateTimeUTC;
                }

                return rv;
            }
        }
    }
}
