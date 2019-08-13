using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Zmq.Dto
{
    public class StateReply
    {
        public StateReply()
        {
            Events = new List<EventMessage>();
        }

        public string Subject { get; set; }
        public List<EventMessage> Events { get; set; }
    }
}
