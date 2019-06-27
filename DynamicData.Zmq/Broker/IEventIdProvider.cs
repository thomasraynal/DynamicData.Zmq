using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DynamicData.EventCache;

namespace DynamicData.Broker
{
    public interface IEventIdProvider
    {
        Task Reset();
        IEventId Next(string streamName, string subject);
    }
}
