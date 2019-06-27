using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Broker;
using DynamicData.Dto;
using DynamicData.Event;
using DynamicData.EventCache;
using DynamicData.Shared;

namespace DynamicData.Default
{
    public class InMemoryEventCache : IEventCache
    {

        private readonly IEventIdProvider _eventIdProvider;
        private readonly IEventSerializer _eventSerializer;
        private readonly ConcurrentDictionary<string, SortedList<long,IEventCacheItem>> _cache;

        public InMemoryEventCache(IEventIdProvider eventIdProvider, IEventSerializer eventSerializer)
        {
            _cache = new ConcurrentDictionary<string, SortedList<long, IEventCacheItem>>();
            _eventIdProvider = eventIdProvider;
            _eventSerializer = eventSerializer;
        }

        public Task<IEventId> AppendToStream(string subject, byte[] payload)
        {
            var streamId = _eventSerializer.GetAggregateId(subject);
            var eventID = _eventIdProvider.Next(streamId, subject);

            var stream = _cache.GetOrAdd(streamId, (_) =>
             {
                 return new SortedList<long, IEventCacheItem>();
             });

            stream.Add(eventID.Version, new EventCacheItem()
            {
                Message = payload,
                EventId = eventID
            });

            return Task.FromResult(eventID);

        }
        public Task<IEnumerable<IEventMessage>> GetAllStreams()
        {
            if (_cache.Count == 0) return Task.FromResult(Enumerable.Empty<IEventMessage>());

            var items = _cache.Values.SelectMany(list=> list.Values).Select(cacheItem =>
            {
                return new EventMessage()
                {
                    EventId = cacheItem.EventId,
                    ProducerMessage = _eventSerializer.Serializer.Deserialize<ProducerMessage>(cacheItem.Message)
                };

            });

            return Task.FromResult(items.Cast<IEventMessage>());
        }

        public Task<IEnumerable<IEventMessage>> GetStream(string streamId)
        {
            if (!_cache.ContainsKey(streamId)) return Task.FromResult(Enumerable.Empty<IEventMessage>());

            var items = _cache[streamId].Values.Select(cacheItem =>
             {
                 return new EventMessage()
                 {
                     EventId = cacheItem.EventId,
                     ProducerMessage = _eventSerializer.Serializer.Deserialize<ProducerMessage>(cacheItem.Message)
                 };

             });

            return Task.FromResult(items.Cast<IEventMessage>());
        }

        public async Task<IEnumerable<IEventMessage>> GetStreamBySubject(string subject)
        {
            if (subject == string.Empty) return await GetAllStreams();

            var streamId = _eventSerializer.GetAggregateId(subject);

            if (!_cache.ContainsKey(streamId)) return Enumerable.Empty<IEventMessage>();

            var items = _cache[streamId].Values
                .Where(ev => ev.EventId.Subject.StartsWith(subject) || subject == string.Empty)
                .Select(cacheItem =>
                {
                    return new EventMessage()
                    {
                        EventId = cacheItem.EventId,
                        ProducerMessage = _eventSerializer.Serializer.Deserialize<ProducerMessage>(cacheItem.Message)
                    };

                });

            return items.Cast<IEventMessage>();

        }
    }
}
