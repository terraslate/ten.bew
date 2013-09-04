using i.ten.bew.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace i.ten.bew.Server
{
    public interface IServiceBus
    {
        Task StartAsync(Action startedHandler, Action stoppedHandler);

        Task SendAsync<P>(ServiceBusMessage message, Reference<ReplyTask<P>> replyReference) where P : class;

        Task SendAsync<P>(string message, string data, Reference<ReplyTask<P>> replyReference) where P : class;

        Task SendAsync<P>(string address, byte[] data, DataFormatEnum dataFormat, Reference<ReplyTask<P>> replyReference) where P : class;

        Task SendAsync(ServiceBusMessage message);

        Task SendAsync(string message, string data);

        Task SendAsync(string address, byte[] data, DataFormatEnum dataFormat);

        IMessageProcessor AddMessageProcessor(string address, IMessageProcessor service);

        IMessageProcessor GetMessageProcessor(string address);

        IMessageProcessor RemoveMessageProcessor(string name);

        T AddLocalService<T>(T service);

        object RemoveLocalService(Type type);

        T RemoveLocalService<T>();

        T GetLocalService<T>();

        IEnumerable<string> MessageProcessors { get; }

        string MacAddress { get; }

        int Port { get; }

        string MulticastAddress { get; }
    }
}
