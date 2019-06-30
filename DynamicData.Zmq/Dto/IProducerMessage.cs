using System;

namespace DynamicData.Zmq.Dto
{
    public interface IProducerMessage
    {
        byte[] MessageBytes { get; set; }
        Type MessageType { get; set; }
        string Subject { get; set; }
    }
}