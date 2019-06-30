using System;
using System.Collections.Generic;
using System.Text;
using DynamicData.Zmq.EventCache;

namespace DynamicData.Zmq.Dto
{
    public class EventMessage : IEventMessage
    {
        public IEventId EventId { get; set; }
        public IProducerMessage ProducerMessage { get; set; }
    }
}
