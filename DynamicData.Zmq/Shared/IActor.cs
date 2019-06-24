using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ZeroMQPlayground.DynamicData.Shared
{
    public interface IActor
    {
        Guid Id { get; }
        Task Run();
        Task Destroy();
    }
}
