using System.Collections.Generic;
using System.Threading.Tasks;
using DynamicData.Dto;

namespace DynamicData.EventCache
{
    public interface IEventCache
    {
        Task<IEventId> AppendToStream(string subject, byte[] payload);
        Task<IEnumerable<IEventMessage>> GetAllStreams();
        Task<IEnumerable<IEventMessage>> GetStream(string streamId);
        Task<IEnumerable<IEventMessage>> GetStreamBySubject(string subject);
        Task Clear();

    }
}