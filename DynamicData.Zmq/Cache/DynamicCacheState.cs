using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroMQPlayground.DynamicData.Cache
{
    public enum DynamicCacheState
    {
        NotConnected,
        Disconnected,
        Connected,
        Reconnected
    }
}
