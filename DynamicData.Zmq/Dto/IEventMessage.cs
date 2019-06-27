using DynamicData.EventCache;

namespace DynamicData.Dto
{
    public interface IEventMessage
    {
        IEventId EventId { get; set; }
        IProducerMessage ProducerMessage { get; set; }
    }
}