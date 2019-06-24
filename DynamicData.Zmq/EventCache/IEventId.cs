namespace ZeroMQPlayground.DynamicData.EventCache
{
    public interface IEventId
    {
        string EventStream { get; set; }
        string Id { get; }
        string Subject { get; set; }
        long Version { get; set; }
        long Timestamp { get; set; }

    }
}