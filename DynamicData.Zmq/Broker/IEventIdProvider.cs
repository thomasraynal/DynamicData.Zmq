using System;
using System.Collections.Generic;
using System.Text;
using ZeroMQPlayground.DynamicData.EventCache;

namespace ZeroMQPlayground.DynamicData.Broker
{
    public interface IEventIdProvider
    {
        IEventId Next(string streamName, string subject);
    }
}
