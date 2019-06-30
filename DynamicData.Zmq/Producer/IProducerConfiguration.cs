using System;
using DynamicData.Zmq.Shared;

namespace DynamicData.Zmq.Producer
{
    public interface IProducerConfiguration : ICanCheckHeartbeatConfiguration
    {
        string BrokerEndpoint { get; set; }
    }
}