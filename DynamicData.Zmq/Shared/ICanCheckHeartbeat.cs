using System;
using System.Threading.Tasks;

namespace DynamicData.Zmq.Shared
{
    public interface ICanCheckHeartbeat
    {
        Task<bool> CheckIsAlive(Uri heartbeatEndpoint, TimeSpan heartbeatDelay, TimeSpan heartbeatTimeout);
    }
}
