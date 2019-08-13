using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Zmq.Dto
{
    public readonly struct ProducerMessage
    {
        public ProducerMessage(string subject, byte[] messageBytes, Type messageType)
        {
            Subject = subject;
            MessageBytes = messageBytes;
            MessageType = messageType;
        }

        public string Subject { get;  }
        public byte[] MessageBytes { get;  }
        public Type MessageType { get;  }
    }
}
