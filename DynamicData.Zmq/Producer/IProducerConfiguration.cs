using System;
using DynamicData.Shared;

namespace DynamicData.Producer
{
    public interface IProducerConfiguration : ICanCheckHeartbeatConfiguration
    {
        string BrokerEndpoint { get; set; }
    }
}