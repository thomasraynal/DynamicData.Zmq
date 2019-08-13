using System;

namespace DynamicData.Zmq.Cache
{
    public class DynamicCacheConfiguration : IDynamicCacheConfiguration
    {
        public DynamicCacheConfiguration()
        {
            StateCatchupTimeout = TimeSpan.FromSeconds(10);
            HeartbeatDelay = TimeSpan.FromSeconds(10);
            HeartbeatTimeout = TimeSpan.FromSeconds(10);

            IsStaleTimeout = TimeSpan.MaxValue;
            ZmqHighWatermark = 1000;
            DoStoreEvents = true;

            Subject = string.Empty;

        }

        public DynamicCacheConfiguration(string subscriptionEndpoint, string stateOfTheWorldEndpoint, string hearbeatEndpoint): this()
        {
            SubscriptionEndpoint = subscriptionEndpoint;
            HeartbeatEndpoint = hearbeatEndpoint;
            StateOfTheWorldEndpoint = stateOfTheWorldEndpoint;
        }

        public string Subject { get; set; }
        public int ZmqHighWatermark { get; set; }
        public TimeSpan HeartbeatDelay { get; set; }
        public TimeSpan HeartbeatTimeout { get; set; }
        public TimeSpan StateCatchupTimeout { get; set; }
        public TimeSpan IsStaleTimeout { get; set; }
        public string StateOfTheWorldEndpoint { get; set; }
        public string SubscriptionEndpoint { get; set; }
        public string HeartbeatEndpoint { get; set; }
        public bool UseEventBatching { get; set; }
        public bool DoStoreEvents { get; set; }
    }
}
