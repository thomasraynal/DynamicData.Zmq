using System.Collections.Generic;
using DynamicData.Dto;

namespace DynamicData.Dto
{
    public interface IStateReply
    {
        List<IEventMessage> Events { get; set; }
        string Subject { get; set; }
    }
}