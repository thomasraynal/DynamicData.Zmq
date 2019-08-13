using System;
using System.Collections.Generic;
using System.Text;
using DynamicData.Zmq.EventCache;

namespace DynamicData.Zmq.Dto
{
    public readonly struct EventMessage
    {
        public EventMessage(EventId eventId, ProducerMessage producerMessage)
        {
            EventId = eventId;
            ProducerMessage = producerMessage;
        }

        public EventId EventId { get; }
        public ProducerMessage ProducerMessage { get; }
    }
}
