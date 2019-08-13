using System.Collections.Generic;
using DynamicData.Zmq.Event;

namespace DynamicData.Zmq.Aggregate
{
    public interface IAggregate
    {
        IEnumerable<IEvent> AppliedEvents { get; }
        void Apply(IEvent @event);
        int Version { get; set; }
        bool DoStoreEvents { get; set; }
    }

    public interface IAggregate<TKey> : IAggregate
    {
        TKey Id { get; set; }
        void Apply<TAggregate>(IEvent<TKey, TAggregate> @event) where TAggregate : IAggregate<TKey>;
    }
}
