using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using ZeroMQPlayground.DynamicData.Dto;
using ZeroMQPlayground.DynamicData.EventCache;
using ZeroMQPlayground.DynamicData.Serialization;

namespace ZeroMQPlayground.DynamicData.Event
{

    public class EventSerializer : IEventSerializer
    {
        //todo : handle the zmq All and *
        public const string All = "*";
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

        public IEvent<TKey, TAggregate> ToEvent<TKey, TAggregate>(IEventMessage eventMessage) where TAggregate : IAggregate<TKey>
        {
            var @event = (IEvent<TKey, TAggregate>)Serializer.Deserialize(eventMessage.ProducerMessage.MessageBytes, eventMessage.ProducerMessage.MessageType);
            @event.Version = eventMessage.EventId.Version;
            return @event;
        }

        public IEvent<TKey, TAggregate> ToEvent<TKey, TAggregate>(IEventId eventId, IProducerMessage eventMessage) where TAggregate : IAggregate<TKey>
        {
            var @event = (IEvent<TKey, TAggregate>)Serializer.Deserialize(eventMessage.MessageBytes, eventMessage.MessageType);
            @event.Version = eventId.Version;
            return @event;
        }

        public IProducerMessage ToProducerMessage<TKey, TAggregate>(IEvent<TKey, TAggregate> @event) where TAggregate : IAggregate<TKey>
        {
            //todo : mutate explicitly
            @event.Subject = GetSubject(@event);

            var message = new ProducerMessage()
            {
                MessageBytes = Serializer.Serialize(@event),
                Subject = @event.Subject,
                MessageType = @event.GetType()
            };

            return message;

        }

        public string GetSubject<TKey, TAggregate>(IEvent<TKey, TAggregate> @event) where TAggregate : IAggregate<TKey>
        {
            var tokens = GetTokens(@event.GetType());

            var subject = tokens.Select(token => @event.GetType().GetProperty(token.PropertyInfo.Name).GetValue(@event, null))
                         .Select(obj => null == obj ? All : obj.ToString())
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
