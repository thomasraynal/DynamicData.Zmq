using DynamicData.Zmq.Aggregate;
using System;

namespace DynamicData.Zmq.Event
{
    //todo: string representation of event stream id
    public interface IEvent
    {
        string EventId { get; }
        string Subject { get; set; }
        Type EventType { get; }
        bool CanApply(Type type);
        void Apply(IAggregate aggregate);
        long Version { get; set; }
    }

    public interface IEvent<TKey, TAggregate> : IEvent where TAggregate : IAggregate<TKey>
    {
        TKey EventStreamId { get; }
        void Apply(TAggregate aggregate);
    }
}
