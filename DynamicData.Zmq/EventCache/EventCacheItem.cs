namespace DynamicData.Zmq.EventCache
{
    public class EventCacheItem : IEventCacheItem
    {
        public byte[] Message { get; set; }
        public IEventId EventId { get; set; }
    }
}
