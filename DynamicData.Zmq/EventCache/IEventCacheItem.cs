using ZeroMQPlayground.DynamicData.Shared;

namespace ZeroMQPlayground.DynamicData.EventCache
{
    public interface IEventCacheItem
    {
        byte[] Message { get; set; }
        IEventId EventId { get; set; }
    }
}