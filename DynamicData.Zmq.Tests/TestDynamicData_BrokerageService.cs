using DynamicData.Zmq.Broker;
using DynamicData.Zmq.Default;
using DynamicData.Zmq.Demo;
using DynamicData.Zmq.Dto;
using DynamicData.Zmq.Event;
using DynamicData.Zmq.EventCache;
using DynamicData.Tests;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using System;
using System.Linq;
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

        protected InMemoryEventIdProvider _eventIdProvider;
        protected JsonNetSerializer _serializer;
        protected EventSerializer _eventSerializer;
        protected InMemoryEventCache _eventCache;

        private BrokerageService _broker;

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _broker.Destroy();
            NetMQConfig.Cleanup(false);
        }

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _eventIdProvider = new InMemoryEventIdProvider();
            _serializer = new JsonNetSerializer();
            _eventSerializer = new EventSerializer(_serializer);
            _eventCache = new InMemoryEventCache(_eventIdProvider, _eventSerializer);

            var brokerConfiguration = new BrokerageServiceConfiguration()
            {
                HeartbeatEndpoint = HeartbeatEndpoint,
                StateOfTheWorldEndpoint = StateOfTheWorldEndpoint,
                ToSubscribersEndpoint = ToSubscribersEndpoint,
                ToPublisherEndpoint = ToPublishersEndpoint
            };

            _broker = new BrokerageService(brokerConfiguration, LoggerForTests<BrokerageService>.Default(), _eventCache, _serializer);

            await _broker.Run();

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

            await Task.Delay(1000);
        }

        [SetUp]
        public async Task SetUp()
        {
            await _eventIdProvider.Reset();
            await _eventCache.Clear();

        }

        //[TearDown]
        //public async Task TearDown()
        //{
        //    await Task.Delay(1000);
        //}

        [Test]
        public void ShouldHandleHeartbeat()
        {
            using (var heartbeat = new RequestSocket(HeartbeatEndpoint))
            {
                var payload = _eventSerializer.Serializer.Serialize(Heartbeat.Query);

                heartbeat.SendFrame(payload);

                var hasResponse = heartbeat.TryReceiveFrameBytes(TimeSpan.FromMilliseconds(1000), out var responseBytes);

                Assert.IsTrue(hasResponse);

                var response = _serializer.Deserialize<Heartbeat>(responseBytes);

                Assert.IsNotNull(response);
                Assert.AreEqual(HeartbeatType.Pong, response.Type);

            }
        }

   

        [Test]
        public async Task ShouldHandleIncomingEvent()
        {
            using (var publisher = new PublisherSocket())
            {
             
                publisher.Connect(ToPublishersEndpoint);

                using (var subscriber = new SubscriberSocket())
                {
                    subscriber.Connect(ToSubscribersEndpoint);
                    subscriber.SubscribeToAnyTopic();

                    var command = new ChangeCcyPairState("EUR/USD", "TEST", CcyPairState.Passive);
                    var message = _eventSerializer.ToProducerMessage(command);

                    await Task.Delay(200);

                    publisher.SendMoreFrame(message.Subject)
                             .SendFrame(_eventSerializer.Serializer.Serialize(message));

                    NetMQMessage msg = null;

                    var hasResponse = subscriber.TryReceiveMultipartMessage(TimeSpan.FromMilliseconds(1000), ref msg);

                    Assert.IsTrue(hasResponse);

                    var eventIdBytes = msg[1].Buffer;
                    var eventMessageBytes = msg[2].Buffer;

                    var eventId = _eventSerializer.Serializer.Deserialize<IEventId>(eventIdBytes);

                    Assert.AreEqual("EUR/USD", eventId.EventStream);
                    Assert.AreEqual("EUR/USD.0", eventId.Id);
                    Assert.AreEqual(0, eventId.Version);
                    Assert.AreEqual(message.Subject, eventId.Subject);

                    var producerMessage = _eventSerializer.Serializer.Deserialize<IProducerMessage>(eventMessageBytes);

                    var @event = _serializer.Deserialize<ChangeCcyPairState>(producerMessage.MessageBytes);
                    
                    Assert.AreEqual(message.Subject, producerMessage.Subject);
                    Assert.AreEqual(typeof(ChangeCcyPairState), producerMessage.MessageType);

                    Assert.AreEqual(command.EventStreamId, @event.EventStreamId);
                    Assert.AreEqual(command.Market, @event.Market);
                    Assert.AreEqual(command.State, @event.State);

                    command = new ChangeCcyPairState("EUR/USD", "TEST", CcyPairState.Active);
                    message = _eventSerializer.ToProducerMessage(command);

                    await Task.Delay(200);

                    publisher.SendMoreFrame(message.Subject)
                             .SendFrame(_eventSerializer.Serializer.Serialize(message));

                    await Task.Delay(50);

                    hasResponse = subscriber.TryReceiveMultipartMessage(TimeSpan.FromMilliseconds(1000), ref msg);

                    Assert.IsTrue(hasResponse);

                    eventIdBytes = msg[1].Buffer;
                    eventMessageBytes = msg[2].Buffer;

                    eventId = _eventSerializer.Serializer.Deserialize<IEventId>(eventIdBytes);

                    Assert.AreEqual("EUR/USD", eventId.EventStream);
                    Assert.AreEqual("EUR/USD.1", eventId.Id);
                    Assert.AreEqual(1, eventId.Version);
                    Assert.AreEqual(message.Subject, eventId.Subject);

                }
            }
        }

        [Test]
        public async Task ShouldHandleStateOfTheWorldRequest()
        {
            using (var publisher = new PublisherSocket())
            {

                publisher.Connect(ToPublishersEndpoint);

                var command1 = new ChangeCcyPairState("EUR/USD", "TEST2", CcyPairState.Passive);
                var command2 = new ChangeCcyPairState("EUR/USD", "TEST2", CcyPairState.Active);

                var message1 = _eventSerializer.ToProducerMessage(command1);
                var message2 = _eventSerializer.ToProducerMessage(command2);

                await Task.Delay(200);

                publisher.SendMoreFrame(message1.Subject)
                         .SendFrame(_eventSerializer.Serializer.Serialize(message1));

                await Task.Delay(200);

                publisher.SendMoreFrame(message2.Subject)
                  .SendFrame(_eventSerializer.Serializer.Serialize(message2));

                await Task.Delay(200);

                var cacheItems = await _eventCache.GetAllStreams();

                Assert.AreEqual(2, cacheItems.Count());

                using (var dealer = new DealerSocket())
                {
                    var request = new StateRequest()
                    {
                        Subject = "EUR/USD"
                    };

                    var requestBytes = _eventSerializer.Serializer.Serialize(request);

                    dealer.Connect(StateOfTheWorldEndpoint);
                    dealer.SendFrame(requestBytes);

                    var hasResponse = dealer.TryReceiveFrameBytes(TimeSpan.FromMilliseconds(1000), out var responseBytes);

                    var response = _eventSerializer.Serializer.Deserialize<StateReply>(responseBytes);

                    Assert.AreEqual(2, response.Events.Count());

                }
            }
        }
 
    }
}
