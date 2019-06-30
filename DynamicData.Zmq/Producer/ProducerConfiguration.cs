using System;

namespace DynamicData.Zmq.Producer
{
    public class ProducerConfiguration : IProducerConfiguration
    {
        public ProducerConfiguration()
        {
            HeartbeatDelay = TimeSpan.FromSeconds(10);
            HeartbeatTimeout = TimeSpan.FromSeconds(1);
        }

        public string BrokerEndpoint { get; set; }
        public string HeartbeatEndpoint { get; set; }
        public TimeSpan HeartbeatDelay { get; set; }
        public TimeSpan HeartbeatTimeout { get; set; }
    }
}
