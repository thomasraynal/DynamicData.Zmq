using DynamicData.Demo;
using DynamicData.Dto;
using DynamicData.Event;
using DynamicData.EventCache;
using NetMQ;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.Zmq.Tests
{
    [TestFixture]
    public class TestDynamicData_BrokerageService
    {
        private readonly string ToPublishersEndpoint = "tcp://localhost:8080";
        private readonly string ToSubscribersEndpoint = "tcp://localhost:8181";
        private readonly string HeartbeatEndpoint = "tcp://localhost:8282";
        private readonly string StateOfTheWorldEndpoint = "tcp://localhost:8383";

        private JsonNetSerializer _serializer;
        private EventSerializer _eventSerializer;

        private readonly Random _rand = new Random();

        [TearDown]
        public void TearDown()
        {
            NetMQConfig.Cleanup(false);
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _serializer = new JsonNetSerializer();
            _eventSerializer = new EventSerializer(_serializer);

            JsonConvert.DefaultSettings = () =>
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    TypeNameHandling = TypeNameHandling.Objects,
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };

                settings.Converters.Add(new AbstractConverter<IEventMessage, EventMessage>());
                settings.Converters.Add(new AbstractConverter<IProducerMessage, ProducerMessage>());
                settings.Converters.Add(new AbstractConverter<IEventId, EventId>());
                settings.Converters.Add(new AbstractConverter<IStateReply, StateReply>());
                settings.Converters.Add(new AbstractConverter<IStateRequest, StateRequest>());

                return settings;
            };
        }

        [Test]
        public async Task ShouldHandleHeartbeat()
        {
            throw new NotImplementedException();
        }

        [Test]
        public async Task ShouldHandleIncomingEvent()
        {
            throw new NotImplementedException();
        }

        [Test]
        public async Task ShouldPublishEvent()
        {
            throw new NotImplementedException();
        }

        [Test]
        public async Task ShouldHandleStateOfTheWorldRequest()
        {
            throw new NotImplementedException();
        }

    }
}
