using System.Collections.Generic;
using DynamicData.Zmq.Event;

namespace DynamicData.Zmq.Aggregate
{
    public interface IAggregate
    {
        IEnumerable<IEvent> AppliedEvents { get; }
        void Apply(IEvent @event);
    }

    public interface IAggregate<TKey> : IAggregate
    {
        //to do : enforce seggregation on setters
        TKey Id { get; set; }
        int Version { get; set; }
        void Apply<TAggregate>(IEvent<TKey, TAggregate> @event) where TAggregate : IAggregate<TKey>;
    }
}
