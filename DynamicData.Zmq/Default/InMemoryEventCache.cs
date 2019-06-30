using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DynamicData.Zmq.Broker;
using DynamicData.Zmq.Dto;
using DynamicData.Zmq.Event;
using DynamicData.Zmq.EventCache;

namespace DynamicData.Zmq.Default
{
    public class InMemoryEventCache : IEventCache
    {

        private readonly IEventIdProvider _eventIdProvider;
        private readonly IEventSerializer _eventSerializer;
        private readonly ConcurrentDictionary<string, SortedList<long,IEventCacheItem>> _cache;
        private readonly object _lock = new object();

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

            //todo : should have a lock per streamId
            lock (_lock)
            {
                stream.Add(eventID.Version, new EventCacheItem()
                {
                    Message = payload,
                    EventId = eventID
                });
            }

            return Task.FromResult(eventID);

        }

        public Task Clear()
        {
            _cache.Clear();

            return Task.CompletedTask;
        }

        public Task<IEnumerable<IEventMessage>> GetAllStreams()
        {
            if (_cache.Count == 0) return Task.FromResult(Enumerable.Empty<IEventMessage>());

            lock (_lock)
            {
                var items = _cache.Values.SelectMany(list => list.Values).Select(cacheItem =>
             {
                 return new EventMessage()
                 {
                     EventId = cacheItem.EventId,
                     ProducerMessage = _eventSerializer.Serializer.Deserialize<ProducerMessage>(cacheItem.Message)
                 };

             })
             .Cast<IEventMessage>()
             .ToList();

                //AsEnumerable is important
                return Task.FromResult(items.AsEnumerable());

            }
        }

        public Task<IEnumerable<IEventMessage>> GetStream(string streamId)
        {
            if (!_cache.ContainsKey(streamId)) return Task.FromResult(Enumerable.Empty<IEventMessage>());

            lock (_lock)
            {
                var items = _cache[streamId].Values.Select(cacheItem =>
                 {
                     return new EventMessage()
                     {
                         EventId = cacheItem.EventId,
                         ProducerMessage = _eventSerializer.Serializer.Deserialize<ProducerMessage>(cacheItem.Message)
                     };

                 })
               .Cast<IEventMessage>()
               .ToList();

                //AsEnumerable is important
                return Task.FromResult(items.AsEnumerable());
            }
        }

        public async Task<IEnumerable<IEventMessage>> GetStreamBySubject(string subject)
        {
            if (subject == string.Empty) return await GetAllStreams();

            var streamId = _eventSerializer.GetAggregateId(subject);

            if (!_cache.ContainsKey(streamId)) return Enumerable.Empty<IEventMessage>();

            lock (_lock)
            {
                var items = _cache[streamId].Values
                    .Where(ev => ev.EventId.Subject.StartsWith(subject) || subject == string.Empty)
                    .Select(cacheItem =>
                    {
                        return new EventMessage()
                        {
                            EventId = cacheItem.EventId,
                            ProducerMessage = _eventSerializer.Serializer.Deserialize<ProducerMessage>(cacheItem.Message)
                        };

                    })
                    .Cast<IEventMessage>()
                    .ToList();

                return items.AsEnumerable();
            }



        }
    }
}
