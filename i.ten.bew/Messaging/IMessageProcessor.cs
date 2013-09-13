﻿using i.ten.bew.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace i.ten.bew.Messaging
{
    public interface IMessageProcessor
    {
        object ProcessMessage(ServiceBusMessage message);

        Task<object> ProcessMessageAsync(ServiceBusMessage message);

        long Processed { get; }

        long Active { get; }
    }
}
