using ZeroMQPlayground.DynamicData.EventCache;

namespace ZeroMQPlayground.DynamicData.Dto
{
    public interface IEventMessage
    {
        IEventId EventId { get; set; }
        IProducerMessage ProducerMessage { get; set; }
    }
}