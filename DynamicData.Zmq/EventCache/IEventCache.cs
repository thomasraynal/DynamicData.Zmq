using System.Collections.Generic;
using System.Threading.Tasks;
using DynamicData.Zmq.Dto;

namespace DynamicData.Zmq.EventCache
{
    public interface IEventCache
    {
        ValueTask<EventId> AppendToStream(byte[] subject, byte[] payload);
        ValueTask<IEnumerable<EventMessage>> GetAllStreams();
        ValueTask<IEnumerable<EventMessage>> GetStream(string streamId);
        ValueTask<IEnumerable<EventMessage>> GetStreamBySubject(string subject);
        ValueTask Clear();

    }
}