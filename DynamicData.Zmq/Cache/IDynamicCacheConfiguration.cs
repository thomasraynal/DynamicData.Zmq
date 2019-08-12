using System;
using DynamicData.Zmq.Shared;

namespace DynamicData.Zmq.Cache
{
    public interface IDynamicCacheConfiguration : ICanCheckHeartbeatConfiguration
    {
        TimeSpan IsStaleTimeout { get; set; }
        TimeSpan StateCatchupTimeout { get; set; }
        string StateOfTheWorldEndpoint { get; set; }
        string Subject { get; set; }
        string SubscriptionEndpoint { get; set; }
        int ZmqHighWatermark { get; set; }
        bool UseEventBatching { get; set; }
    }
}