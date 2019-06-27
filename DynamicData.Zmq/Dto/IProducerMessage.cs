using System;

namespace DynamicData.Dto
{
    public interface IProducerMessage
    {
        byte[] MessageBytes { get; set; }
        Type MessageType { get; set; }
        string Subject { get; set; }
    }
}