namespace ZeroMQPlayground.DynamicData.Broker
{
    public interface IBrokerageServiceConfiguration
    {
        string HeartbeatEndpoint { get; set; }
        string StateOftheWorldEndpoint { get; set; }
        string ToPublisherEndpoint { get; set; }
        string ToSubscribersEndpoint { get; set; }
    }
}