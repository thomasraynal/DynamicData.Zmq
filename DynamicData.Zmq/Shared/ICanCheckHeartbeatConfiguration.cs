using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.Shared
{
    public interface ICanCheckHeartbeatConfiguration
    {
        string HeartbeatEndpoint { get; set; }
        TimeSpan HeartbeatDelay { get; set; }
        TimeSpan HeartbeatTimeout { get; set; }
    }
}
