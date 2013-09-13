using i.ten.bew;
using i.ten.bew.Messaging;
using i.ten.bew.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace i.ten.bew.Messaging
{
    public abstract class MessageProcessorBase : IMessageProcessor
    {
        private long _processed;
        private long _active;
        private PropertyInfo[] _properties;

        public MessageProcessorBase()
        {
            _properties = GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.FlattenHierarchy);
        }

        public long Processed
        {
            get
            {
                return Interlocked.Read(ref _processed);
            }
        }
        public long Active
        {
            get
            {
                return Interlocked.Read(ref _active);
            }
        }

        public object ProcessMessage(ServiceBusMessage message)
        {
            var task = ProcessMessageAsync(message);
            task.Wait();
            return task.Result;
        }

        public async Task<object> ProcessMessageAsync(ServiceBusMessage message)
        {
            Interlocked.Increment(ref _processed);
            object rv = null;

            try
            {
                Interlocked.Increment(ref _active);
                rv = await InternalProcessMessageAsync(message);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }

            return rv;
        }

        protected async virtual Task<object> InternalProcessMessageAsync(ServiceBusMessage message)
        {
            return null;
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
