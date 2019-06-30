using System.Threading.Tasks;
using DynamicData.Zmq.EventCache;

namespace DynamicData.Zmq.Broker
{
    public interface IEventIdProvider
    {
        Task Reset();
        IEventId Next(string streamName, string subject);
    }
}
