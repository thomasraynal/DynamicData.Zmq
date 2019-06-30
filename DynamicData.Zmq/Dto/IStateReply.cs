using System.Collections.Generic;
using DynamicData.Zmq.Dto;

namespace DynamicData.Zmq.Dto
{
    public interface IStateReply
    {
        List<IEventMessage> Events { get; set; }
        string Subject { get; set; }
    }
}