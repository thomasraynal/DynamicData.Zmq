using System.Collections.Generic;
using DynamicData.Zmq.Aggregate;
using DynamicData.Zmq.Event;

namespace DynamicData.Zmq.Cache
{
    public class CaughtingUpCache<TKey, TAggregate> where TAggregate : IAggregate<TKey>, new()
    {
        public CaughtingUpCache()
        {
            CaughtUpEvents = new List<IEvent<TKey, TAggregate>>();
        }

        public List<IEvent<TKey, TAggregate>> CaughtUpEvents { get; }

        public void Clear()
        {
            CaughtUpEvents.Clear();
        }
    }
}
