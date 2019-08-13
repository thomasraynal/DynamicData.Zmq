using DynamicData.Zmq.Dto;
using DynamicData.Zmq.EventCache;
using DynamicData.Zmq.Serialization;
using DynamicData.Zmq.Aggregate;

namespace DynamicData.Zmq.Event
{
    public interface IEventSerializer
    {
        ISerializer Serializer { get; }
        string GetAggregateId(string subject);
        string GetSubject<TKey, TAggregate>(IEvent<TKey, TAggregate> @event) where TAggregate : IAggregate<TKey>;
        IEvent<TKey, TAggregate> ToEvent<TKey, TAggregate>(in EventMessage eventMessage) where TAggregate : IAggregate<TKey>;
        IEvent<TKey, TAggregate> ToEvent<TKey, TAggregate>(in EventId eventId, in ProducerMessage eventMessage) where TAggregate : IAggregate<TKey>;
        ProducerMessage ToProducerMessage<TKey, TAggregate>(IEvent<TKey, TAggregate> @event) where TAggregate : IAggregate<TKey>;
    }
}