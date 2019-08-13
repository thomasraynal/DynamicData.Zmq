using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DynamicData.Zmq.Aggregate;
using DynamicData.Zmq.Dto;
using DynamicData.Zmq.EventCache;
using DynamicData.Zmq.Serialization;

namespace DynamicData.Zmq.Event
{

    public class EventSerializer : IEventSerializer
    {

        public const char Separator = '.';

        public ISerializer Serializer { get; }

        public EventSerializer(ISerializer serializer)
        {
            Serializer = serializer;
        }

        public string GetAggregateId(string subject)
        {
            return subject.Split(Separator).First();
        }

        public IEvent<TKey, TAggregate> ToEvent<TKey, TAggregate>(in EventMessage eventMessage) where TAggregate : IAggregate<TKey>
        {
            var @event = (IEvent<TKey, TAggregate>)Serializer.Deserialize(eventMessage.ProducerMessage.MessageBytes, eventMessage.ProducerMessage.MessageType);
            @event.Version = eventMessage.EventId.Version;
            return @event;
        }

        public IEvent<TKey, TAggregate> ToEvent<TKey, TAggregate>(in EventId eventId, in ProducerMessage eventMessage) where TAggregate : IAggregate<TKey>
        {
            var @event = (IEvent<TKey, TAggregate>)Serializer.Deserialize(eventMessage.MessageBytes, eventMessage.MessageType);
            @event.Version = eventId.Version;
            return @event;
        }

        public ProducerMessage ToProducerMessage<TKey, TAggregate>(IEvent<TKey, TAggregate> @event) where TAggregate : IAggregate<TKey>
        {
            @event.Subject = GetSubject(@event);

            return new ProducerMessage(@event.Subject, Serializer.Serialize(@event), @event.GetType());

        }

        public string GetSubject<TKey, TAggregate>(IEvent<TKey, TAggregate> @event) where TAggregate : IAggregate<TKey>
        {
            var tokens = GetTokens(@event.GetType());

            var subject = tokens.Select(token => @event.GetType().GetProperty(token.PropertyInfo.Name).GetValue(@event, null))
                         .Select(obj => obj?.ToString())
                         .Where(str => str != null)
                         .Aggregate((token1, token2) => $"{token1}{Separator}{token2}");

            return $"{@event.EventStreamId}.{subject}";

        }

        private IEnumerable<PropertyToken> GetTokens(Type messageType)
        {

            var properties = messageType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                    .Select(prop => new { attributes = prop.GetCustomAttributes(typeof(RoutingPositionAttribute), true), property = prop })
                                    .Where(selection => selection.attributes.Count() > 0)
                                    .Select(selection => new PropertyToken(((RoutingPositionAttribute)selection.attributes[0]).Position, messageType, selection.property));

            return properties.OrderBy(x => x.Position).ToArray();
        }
    }
}
