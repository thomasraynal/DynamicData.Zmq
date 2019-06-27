using System;
using System.Collections.Generic;
using System.Text;
using DynamicData.Event;
using DynamicData.Shared;

namespace DynamicData.Cache
{
    public class CaughtingUpCache<TKey, TAggregate> where TAggregate : IAggregate<TKey>, new()
    {
        public CaughtingUpCache()
        {
            CaughtUpEvents = new List<IEvent<TKey, TAggregate>>();
        }

        public List<IEvent<TKey, TAggregate>> CaughtUpEvents { get; set; }

        public void Clear()
        {
            CaughtUpEvents.Clear();
        }
    }
}
