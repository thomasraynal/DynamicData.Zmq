using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroMQPlayground.DynamicData.Broker
{
    public class BrokerageServiceConfiguration : IBrokerageServiceConfiguration
    {
        public string ToPublisherEndpoint { get; set; }
        public string ToSubscribersEndpoint { get; set; }
        public string StateOftheWorldEndpoint { get; set; }
        public string HeartbeatEndpoint { get; set; }
    }
}
