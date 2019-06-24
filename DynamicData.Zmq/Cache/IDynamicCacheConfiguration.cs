using System;
using ZeroMQPlayground.DynamicData.Shared;

namespace ZeroMQPlayground.DynamicData.Cache
{
    public interface IDynamicCacheConfiguration : IHeartbeatChecker
    {
        TimeSpan IsStaleTimeout { get; set; }
        TimeSpan StateCatchupTimeout { get; set; }
        string StateOfTheWorldEndpoint { get; }
        string Subject { get; set; }
        string SubscriptionEndpoint { get; }
        int ZmqHighWatermark { get; set; }
    }
}