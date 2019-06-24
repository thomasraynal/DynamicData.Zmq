using System;
using System.Collections.Generic;
using System.Text;
using ZeroMQPlayground.DynamicData.Broker;
using ZeroMQPlayground.DynamicData.EventCache;

namespace ZeroMQPlayground.DynamicData.Default
{
    public class InMemoryEventIdProvider : IEventIdProvider
    {
        private readonly Dictionary<string, long> _eventStreamsVersionGenerator;
        private readonly List<IEventId> _eventIds;

        public InMemoryEventIdProvider()
        {
            _eventStreamsVersionGenerator = new Dictionary<string, long>();
            _eventIds = new List<IEventId>();
        }

        public IEventId Next(string streamName, string subject)
        {
            var version = -1L;

            if (!_eventStreamsVersionGenerator.ContainsKey(streamName))
            {
                _eventStreamsVersionGenerator.Add(streamName, ++version);
            }
            else
            {
                version = ++_eventStreamsVersionGenerator[streamName];
            }

            var eventId = new EventId()
            {
                EventStream = streamName,
                Subject = subject,
                Version = version,
                Timestamp = DateTime.Now.Ticks
            };

            _eventIds.Add(eventId);

            return eventId;
        }
    }
}
