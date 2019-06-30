using System;

namespace DynamicData.Zmq.Shared
{
    public interface ICanCheckHeartbeatConfiguration
    {
        string HeartbeatEndpoint { get; set; }
        TimeSpan HeartbeatDelay { get; set; }
        TimeSpan HeartbeatTimeout { get; set; }
    }
}
