using System.Collections.Generic;
using ZeroMQPlayground.DynamicData.Dto;

namespace ZeroMQPlayground.DynamicData.Dto
{
    public interface IStateReply
    {
        List<IEventMessage> Events { get; set; }
        string Subject { get; set; }
    }
}