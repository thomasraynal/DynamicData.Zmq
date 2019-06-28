using System;
using DynamicData.Shared;

namespace DynamicData.Cache
{
    public interface IDynamicCacheConfiguration : ICanCheckHeartbeatConfiguration
    {
        TimeSpan IsStaleTimeout { get; set; }
        TimeSpan StateCatchupTimeout { get; set; }
        string StateOfTheWorldEndpoint { get; set; }
        string Subject { get; set; }
        string SubscriptionEndpoint { get; set; }
        int ZmqHighWatermark { get; set; }
    }
}