using System;
using System.Collections.Generic;
using System.Text;
using DynamicData.Shared;

namespace DynamicData.EventCache
{
    public class EventCacheItem : IEventCacheItem
    {
        public byte[] Message { get; set; }
        public IEventId EventId { get; set; }
    }
}
