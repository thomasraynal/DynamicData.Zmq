using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Zmq.Dto
{
    public class StateReply : IStateReply
    {
        public StateReply()
        {
            Events = new List<IEventMessage>();
        }

        public string Subject { get; set; }
        public List<IEventMessage> Events { get; set; }
    }
}
