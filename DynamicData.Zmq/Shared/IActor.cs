using System;
using System.Threading.Tasks;

namespace DynamicData.Zmq.Shared
{
    public interface IActor
    {
        Guid Id { get; }
        Task Run();
        Task Destroy();
        ActorState State { get;  }
    }
}
