namespace DynamicData.Zmq.Broker
{
    public class BrokerageServiceConfiguration : IBrokerageServiceConfiguration
    {
        public BrokerageServiceConfiguration()
        {
            ZmqHighWatermark = 1000;
        }

        public string ToPublisherEndpoint { get; set; }
        public string ToSubscribersEndpoint { get; set; }
        public string StateOfTheWorldEndpoint { get; set; }
        public string HeartbeatEndpoint { get; set; }
        public int ZmqHighWatermark { get; set; }
    }
}