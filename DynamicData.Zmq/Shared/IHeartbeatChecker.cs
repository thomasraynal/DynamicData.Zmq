using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroMQPlayground.DynamicData.Shared
{
    public interface IHeartbeatChecker
    {
        string HearbeatEndpoint { get; set; }
        TimeSpan HeartbeatDelay { get; set; }
        TimeSpan HeartbeatTimeout { get; set; }
    }
}
