using System;
using System.Threading.Tasks;

namespace DynamicData.Zmq.Shared
{
    public interface IActor : IDisposable
    {
        Guid Id { get; }
        Task Run();
        Task Destroy();
        ActorState State { get;  }
    }
}
