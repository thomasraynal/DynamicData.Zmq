using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroMQPlayground.DynamicData.Dto
{
    public class ProducerMessage : IProducerMessage
    {
        public string Subject { get; set; }
        public byte[] MessageBytes { get; set; }
        public Type MessageType { get; set; }
    }
}
