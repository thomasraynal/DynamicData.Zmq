using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Zmq.Broker;
using DynamicData.Zmq.EventCache;

namespace DynamicData.Zmq.Default
{
    public class InMemoryEventIdProvider : IEventIdProvider
    {
        private readonly Dictionary<string, long> _eventStreamsVersionGenerator;

        public InMemoryEventIdProvider()
        {
            _eventStreamsVersionGenerator = new Dictionary<string, long>();
        }

        public EventId Next(string streamName, string subject)
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

            return new EventId(streamName, version, subject, DateTime.Now.Ticks);
        }

        public Task Reset()
        {
            _eventStreamsVersionGenerator.Clear();

            return Task.CompletedTask;
        }
    }
}
