using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroMQPlayground.DynamicData.Producer
{
    public class ProducerConfiguration : IProducerConfiguration
    {
        public ProducerConfiguration()
        {
            HeartbeatDelay = TimeSpan.FromSeconds(10);
            HeartbeatTimeout = TimeSpan.FromSeconds(10);
        }

        public string RouterEndpoint { get; set; }
        public string HearbeatEndpoint { get; set; }
        public TimeSpan HeartbeatDelay { get; set; }
        public TimeSpan HeartbeatTimeout { get; set; }
    }
}
