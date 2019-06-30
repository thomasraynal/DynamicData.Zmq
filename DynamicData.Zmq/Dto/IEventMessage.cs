using DynamicData.Zmq.EventCache;

namespace DynamicData.Zmq.Dto
{
    public interface IEventMessage
    {
        IEventId EventId { get; set; }
        IProducerMessage ProducerMessage { get; set; }
    }
}