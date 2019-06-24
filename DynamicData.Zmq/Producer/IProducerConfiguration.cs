using System;
using ZeroMQPlayground.DynamicData.Shared;

namespace ZeroMQPlayground.DynamicData.Producer
{
    public interface IProducerConfiguration : IHeartbeatChecker
    {
        string RouterEndpoint { get; set; }
    }
}