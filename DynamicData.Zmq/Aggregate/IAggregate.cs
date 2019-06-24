using System;
using System.Collections.Generic;
using System.Text;

using ZeroMQPlayground.DynamicData.Aggregate;
using ZeroMQPlayground.DynamicData.Event;

namespace ZeroMQPlayground.DynamicData
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
