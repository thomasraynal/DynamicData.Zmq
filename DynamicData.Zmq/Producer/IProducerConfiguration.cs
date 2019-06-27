using System;
using DynamicData.Shared;

namespace DynamicData.Producer
{
    public interface IProducerConfiguration : IHeartbeatChecker
    {
        string RouterEndpoint { get; set; }
    }
}