namespace DynamicData.Zmq.EventCache
{
    public interface IEventCacheItem
    {
        byte[] Message { get; set; }
        IEventId EventId { get; set; }
    }
}