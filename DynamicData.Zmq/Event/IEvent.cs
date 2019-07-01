using DynamicData.Zmq.Aggregate;
using System;

namespace DynamicData.Zmq.Event
{
  
    public interface IEvent
    {
        string EventId { get; }
        string Subject { get; set; }
        Type EventType { get; }
        bool CanApply(Type type);
        void Apply(IAggregate aggregate);
        long Version { get; set; }
    }

    //application convention: TKey must have a distinct string serialization
    public interface IEvent<TKey, TAggregate> : IEvent where TAggregate : IAggregate<TKey>
    {
        TKey EventStreamId { get; }
        void Apply(TAggregate aggregate);
    }
}
