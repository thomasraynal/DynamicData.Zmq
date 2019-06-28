namespace DynamicData.Broker
{
    public interface IBrokerageServiceConfiguration
    {
        string HeartbeatEndpoint { get; set; }
        string StateOfTheWorldEndpoint { get; set; }
        string ToPublisherEndpoint { get; set; }
        string ToSubscribersEndpoint { get; set; }
        int ZmqHighWatermark { get; set; }
    }
}