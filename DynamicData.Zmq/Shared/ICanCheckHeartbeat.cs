using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.Shared
{
    public interface ICanCheckHeartbeat
    {
        Task<bool> CheckIsAlive(Uri heartbeatEndpoint, TimeSpan heartbeatDelay, TimeSpan heartbeatTimeout);
    }
}
