using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly ConcurrentDictionary<string, SortedList<long, EventCacheItem>> _cache;
        private readonly object _lock = new object();

        private readonly ValueTask CompletedTask = new ValueTask(Task.CompletedTask);

        public InMemoryEventCache(IEventIdProvider eventIdProvider, IEventSerializer eventSerializer)
        {
            _cache = new ConcurrentDictionary<string, SortedList<long, EventCacheItem>>();
            _eventIdProvider = eventIdProvider;
            _eventSerializer = eventSerializer;
        }

        public ValueTask<EventId> AppendToStream(byte[] subject, byte[] payload)
        {
            var subjectAsString = Encoding.UTF8.GetString(subject);

            var streamId = _eventSerializer.GetAggregateId(subjectAsString);
            var eventID = _eventIdProvider.Next(streamId, subjectAsString);

            var stream = _cache.GetOrAdd(streamId, (_) =>
             {
                 return new SortedList<long, EventCacheItem>();
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

            return new ValueTask<EventId>(eventID);

        }

        public ValueTask Clear()
        {
            _cache.Clear();

            return CompletedTask;
        }

        public ValueTask<IEnumerable<EventMessage>> GetAllStreams()
        {
            if (_cache.Count == 0) return new ValueTask<IEnumerable<EventMessage>>(Enumerable.Empty<EventMessage>());

            lock (_lock)
            {
                var items = _cache.Values.SelectMany(list => list.Values).Select(cacheItem =>
             {
                 return new EventMessage(cacheItem.EventId, _eventSerializer.Serializer.Deserialize<ProducerMessage>(cacheItem.Message));
             })
             .ToList();

                return new ValueTask<IEnumerable<EventMessage>>(items.AsEnumerable());

            }
        }

        public ValueTask<IEnumerable<EventMessage>> GetStream(string streamId)
        {
            if (!_cache.ContainsKey(streamId)) return new ValueTask<IEnumerable<EventMessage>>(Enumerable.Empty<EventMessage>());

            lock (_lock)
            {
                var items = _cache[streamId].Values.Select(cacheItem =>
                 {
                     return new EventMessage(cacheItem.EventId, _eventSerializer.Serializer.Deserialize<ProducerMessage>(cacheItem.Message));
                 })
               .ToList();

                return new ValueTask<IEnumerable<EventMessage>>(items.AsEnumerable());
            }
        }

        public async ValueTask<IEnumerable<EventMessage>> GetStreamBySubject(string subject)
        {
            if (subject == string.Empty) return await GetAllStreams();

            var streamId = _eventSerializer.GetAggregateId(subject);

            if (!_cache.ContainsKey(streamId)) return Enumerable.Empty<EventMessage>();

            lock (_lock)
            {
                var items = _cache[streamId].Values
                    .Where(ev => ev.EventId.Subject.StartsWith(subject) || subject == string.Empty)
                    .Select(cacheItem =>
                    {
                        return new EventMessage(cacheItem.EventId, _eventSerializer.Serializer.Deserialize<ProducerMessage>(cacheItem.Message));
                    })
                    .ToList();

                return items.AsEnumerable();
            }



        }
    }
}
