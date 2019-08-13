namespace DynamicData.Zmq.EventCache
{
    public class EventCacheItem
    {
        public byte[] Message { get; set; }
        public EventId EventId { get; set; }
    }
}
