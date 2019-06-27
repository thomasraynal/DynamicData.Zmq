using DynamicData.Shared;

namespace DynamicData.EventCache
{
    public interface IEventCacheItem
    {
        byte[] Message { get; set; }
        IEventId EventId { get; set; }
    }
}