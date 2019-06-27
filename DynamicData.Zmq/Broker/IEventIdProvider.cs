using System;
using System.Collections.Generic;
using System.Text;
using DynamicData.EventCache;

namespace DynamicData.Broker
{
    public interface IEventIdProvider
    {
        IEventId Next(string streamName, string subject);
    }
}
