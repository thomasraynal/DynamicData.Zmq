using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Zmq.Default;
using DynamicData.Zmq.Demo;
using DynamicData.Zmq.Dto;
using DynamicData.Zmq.Event;
using DynamicData.Zmq.EventCache;
using DynamicData.Zmq.Cache;
using DynamicData.Tests;

namespace DynamicData.Zmq.Tests
{
    [TestFixture]
    public class TestDynamicData_Cache
    {

        private readonly string ToPublishersEndpoint = "tcp://localhost:8080";
        private readonly string ToSubscribersEndpoint = "tcp://localhost:8181";
        private readonly string HeartbeatEndpoint = "tcp://localhost:8282";
        private readonly string StateOfTheWorldEndpoint = "tcp://localhost:8383";

        private JsonNetSerializer _serializer;
        private EventSerializer _eventSerializer;
        private InMemoryEventIdProvider _eventIdProvider;
        private InMemoryEventCache _eventCache;
        private CancellationTokenSource _cancel;

        private readonly Random _rand = new Random();

        public async Task WaitForCachesToCaughtUp(params DynamicCache<string, CurrencyPair>[] caches)
        {
            while (!caches.All(c => !c.IsCaughtingUp))
            {
                await Task.Delay(1000);
            }
        }

        [TearDown]
        public async Task TearDown()
        {
            await _eventIdProvider.Reset();
            await _eventCache.Clear();

            await Task.Delay(1000);
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await DestroyFakeBroker(_cancel);
            NetMQConfig.Cleanup(false);
        }

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _serializer = new JsonNetSerializer();
            _eventSerializer = new EventSerializer(_serializer);
            _eventIdProvider = new InMemoryEventIdProvider();
            _eventCache = new InMemoryEventCache(_eventIdProvider, _eventSerializer);
            _cancel = new CancellationTokenSource();

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

            SetupFakeBroker(_cancel.Token, _eventCache);

            await Task.Delay(1000);
        }

        private ChangeCcyPairPrice Next(string streamId, string market = "TEST")
        {
            var mid = _rand.NextDouble() * 10;
            var spread = _rand.NextDouble() * 2;

            var price = new ChangeCcyPairPrice(
                ask: mid + spread,
                bid: mid - spread,
                mid: mid,
                spread: spread,
                ccyPairId: streamId,
                market: market
            )
            {
                EventStreamId = streamId
            };

            return price;
        }

        private async Task DestroyFakeBroker(CancellationTokenSource cancel)
        {
            cancel.Cancel();
            await Task.Delay(1500);
        }

        private void SetupFakeBroker(
            CancellationToken cancel,
            IEventCache eventCache,
            bool useHeartbeat = true,
            bool useEventLoop = true,
            bool useStateOfTheWorld = true)
        {

            if (useHeartbeat)
            {
                //heartbeat
                Task.Run(() =>
                {
                    using (var heartbeatSocket = new ResponseSocket(HeartbeatEndpoint))
                    {
                        while (!cancel.IsCancellationRequested)
                        {
                            var hasResponse = heartbeatSocket.TryReceiveFrameBytes(TimeSpan.FromSeconds(1), out var messageBytes);

                            if (hasResponse)
                            {
                                heartbeatSocket.SendFrame(_serializer.Serialize(Heartbeat.Response));
                            }
                        }
                    }
                }, cancel);

            }

            if (useEventLoop)
            {
                //event loop
                Task.Run(async () =>
                {
                    using (var stateUpdate = new SubscriberSocket())
                    {
                        stateUpdate.SubscribeToAnyTopic();
                        stateUpdate.Bind(ToPublishersEndpoint);

                        while (!cancel.IsCancellationRequested)
                        {
                            NetMQMessage message = null;

                            var hasResponse = stateUpdate.TryReceiveMultipartMessage(TimeSpan.FromSeconds(1), ref message);

                            if (hasResponse)
                            {
                                var subject = message[0].ConvertToString();
                                var payload = message[1];

                                await eventCache.AppendToStream(subject, payload.Buffer);

                            }

                        }
                    }
                }, cancel);

            }

            if (useStateOfTheWorld)
            {

                //stateOfTheWorld
                Task.Run(async () =>
                {
                    using (var stateRequestSocket = new RouterSocket())
                    {
                        stateRequestSocket.Bind(StateOfTheWorldEndpoint);

                        while (!cancel.IsCancellationRequested)
                        {
                            NetMQMessage message = null;

                            var hasResponse = stateRequestSocket.TryReceiveMultipartMessage(TimeSpan.FromSeconds(1), ref message);

                            if (hasResponse)
                            {
                                var sender = message[0].Buffer;
                                var request = _serializer.Deserialize<IStateRequest>(message[1].Buffer);

                                var stream = await eventCache.GetStreamBySubject(request.Subject);

                                var response = new StateReply()
                                {
                                    Subject = request.Subject,
                                    Events = stream.ToList()
                                };

                                stateRequestSocket.SendMoreFrame(sender)
                                                   .SendFrame(_serializer.Serialize(response));
                            }
                        }
                    }

                }, cancel);

            }
        }

        [Test]
        public async Task ShouldHandleConnectivityErrors()
        {
            var cacheConfiguration = new DynamicCacheConfiguration("tcp://localhost:9090", "tcp://localhost:9090", HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromMilliseconds(500),
                HeartbeatTimeout = TimeSpan.FromMilliseconds(500),
                StateCatchupTimeout = TimeSpan.FromMilliseconds(500)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, LoggerForTests<DynamicCache<string, CurrencyPair>>.Default(), _eventSerializer);

            await cache.Run();

            await Task.Delay(2000);

            Assert.Greater(cache.Errors.Count, 0);

            var error = cache.Errors.First();

            Assert.AreEqual(DynamicCacheErrorType.GetStateOfTheWorldFailure, error.CacheErrorStatus);
            Assert.AreEqual(typeof(UnreachableBrokerException), error.Exception.GetType());
        }

        [Test]
        public async Task ShouldHandleNewEventsWhileRebuildingTheCache()
        {
            using (var stateUpdatePublish = new PublisherSocket())
            {
                stateUpdatePublish.Bind(ToSubscribersEndpoint);

                var topic = "EUR/USD";
                var stateOfTheWorldEventCount = 10000;
                var initialEventCache = 50;
                var raisedEventDuringCacheBuilding = new List<IEventId>();

                for (var i = 0; i < stateOfTheWorldEventCount; i++)
                {
                    var next = Next(topic);
                    var message = _eventSerializer.ToProducerMessage(next);
                    await _eventCache.AppendToStream(next.Subject, _serializer.Serialize(message));
                }

                var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
                {
                    Subject = string.Empty,
                    HeartbeatDelay = TimeSpan.FromSeconds(1),
                    HeartbeatTimeout = TimeSpan.FromSeconds(1)
                };

                var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, LoggerForTests<DynamicCache<string, CurrencyPair>>.Default(), _eventSerializer);

                await cache.Run();

                for (var i = 0; i < initialEventCache; i++)
                {

                    var next = Next(topic);
                    var message = _eventSerializer.ToProducerMessage(next);
                    var payload = _serializer.Serialize(message);
                    var eventId = _eventCache.AppendToStream(next.Subject, payload).Result;

                    raisedEventDuringCacheBuilding.Add(eventId);

                    stateUpdatePublish.SendMoreFrame(next.Subject)
                                      .SendMoreFrame(_serializer.Serialize(eventId))
                                      .SendFrame(payload);

                    await Task.Delay(100);
                }

           

                await Task.Delay(2000);

                Assert.IsFalse(cache.IsCaughtingUp);

                var ccyPairs = cache.Items
                                    .ToList();

                Assert.AreEqual(1, ccyPairs.Count);



                var euroDol = ccyPairs.First();

                Assert.AreEqual(stateOfTheWorldEventCount + initialEventCache, euroDol.AppliedEvents.Count());

                await cache.Destroy();
            }


        }

        [Test]
        public async Task ShouldSwitchToStaledState()
        {
           
            using (var publisherSocket = new PublisherSocket())
            {
                publisherSocket.Bind(ToSubscribersEndpoint);

                var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
                {
                    Subject = string.Empty,
                    HeartbeatDelay = TimeSpan.FromSeconds(5),
                    HeartbeatTimeout = TimeSpan.FromSeconds(1),
                    IsStaleTimeout = TimeSpan.FromSeconds(2)
                };

                var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, LoggerForTests<DynamicCache<string, CurrencyPair>>.Default(), _eventSerializer);

                await cache.Run();

                await WaitForCachesToCaughtUp(cache);

                Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);
                Assert.AreEqual(true, cache.IsStaled);

                var @event = new ChangeCcyPairPrice("TEST", "TEST-Market", 0.0, 0.0, 0.0, 0.0);
                var message = _eventSerializer.ToProducerMessage(@event);

                var eventId = new EventId()
                {
                    EventStream = "TEST",
                    Subject = "TEST",
                    Timestamp = DateTime.Now.Ticks,
                    Version = 0
                };

                publisherSocket.SendMoreFrame(message.Subject)
                               .SendMoreFrame(_serializer.Serialize(eventId))
                               .SendFrame(_serializer.Serialize(message));

                await Task.Delay(200);

                Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);
                Assert.AreEqual(false, cache.IsStaled);

                await Task.Delay(3500);

                Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);
                Assert.AreEqual(true, cache.IsStaled);

                await Task.Delay(2000);

                Assert.AreEqual(true, cache.IsStaled);

                await cache.Destroy();

            }
        }


        [Test]
        public async Task ShouldProduceEventsAndConsumeThemViaEventCache()
        {

            var marketConfiguration = new MarketConfiguration("TEST")
            {
                BrokerEndpoint = ToPublishersEndpoint,
                HeartbeatEndpoint = HeartbeatEndpoint,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var producer = new Market(marketConfiguration, LoggerForTests<Market>.Default(), _eventSerializer);
            await producer.Run();

            await Task.Delay(2000);

            var eventCountOnProducer = producer.Prices.Count;
            var eventsOnEventCache = (await _eventCache.GetAllStreams())
                .Select(msg => _serializer.Deserialize<ChangeCcyPairPrice>(msg.ProducerMessage.MessageBytes))
                .ToList();

            Assert.Greater(eventCountOnProducer, 0);
            Assert.Greater(eventsOnEventCache.Count, 0);
            Assert.AreEqual(eventCountOnProducer, eventsOnEventCache.Count);

            Assert.IsTrue(eventsOnEventCache.All(ev => producer.Prices.Any(p=> p.Ask == ev.Ask)));

            producer.Publish(new ChangeCcyPairPrice("TEST", "TEST-Market", 0.0, 0.0, 0.0, 0.0));

            await Task.Delay(200);

            var testEvent = await _eventCache.GetStream("TEST");

            Assert.AreEqual(1, testEvent.Count());

            await producer.Destroy();

        }

        [Test]
        public async Task ShouldPerformHeartbeat()
        {

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, LoggerForTests<DynamicCache<string, CurrencyPair>>.Default(), _eventSerializer);

            await cache.Run();

            await WaitForCachesToCaughtUp(cache);

            Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);

            await cache.Destroy();
        }

        [Test]
        public async Task ShouldSubscribeToSpecificSubject()
        {
            using (var publisherSocket = new PublisherSocket())
            {
                publisherSocket.Bind(ToSubscribersEndpoint);

                var createEvent = new Func<string, string, Task>(async (streamId, market) =>
                  {
                      var @event = new ChangeCcyPairPrice(streamId, market, 0.0, 0.0, 0.0, 0.0);
                      var message = _eventSerializer.ToProducerMessage(@event);

                      var eventId = new EventId()
                      {
                          EventStream = streamId,
                          Subject = string.IsNullOrEmpty(market) ? streamId : $"{streamId}.{market}",
                          Timestamp = DateTime.Now.Ticks,
                          Version = 0
                      };

                      publisherSocket.SendMoreFrame(message.Subject)
                                     .SendMoreFrame(_serializer.Serialize(eventId))
                                     .SendFrame(_serializer.Serialize(message));

                      await Task.Delay(500);

                  });

                var subscribedToStreamId = "EUR/USD";
                var subscribedToMarket = "Harmony";

                var NOTsubscribedToStreamId = "EUR/GBP";
                var NOTsubscribedToMarket = "FxConnect";

                var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
                {
                    Subject = $"{subscribedToStreamId}.{subscribedToMarket}",
                    HeartbeatDelay = TimeSpan.FromSeconds(1),
                    HeartbeatTimeout = TimeSpan.FromSeconds(1),
                };

                var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, LoggerForTests<DynamicCache<string, CurrencyPair>>.Default(), _eventSerializer);

                await cache.Run();

                await Task.Delay(1000);

                await createEvent(NOTsubscribedToStreamId, NOTsubscribedToMarket);

                Assert.AreEqual(0, cache.Items.SelectMany(ccy => ccy.AppliedEvents).Count());

                await createEvent(NOTsubscribedToStreamId, subscribedToMarket);

                Assert.AreEqual(0, cache.Items.SelectMany(ccy => ccy.AppliedEvents).Count());

                await createEvent(subscribedToStreamId, NOTsubscribedToMarket);

                Assert.AreEqual(0, cache.Items.SelectMany(ccy => ccy.AppliedEvents).Count());

                await createEvent(subscribedToStreamId, subscribedToMarket);

                Assert.AreEqual(1, cache.Items.SelectMany(ccy => ccy.AppliedEvents).Count());

                await createEvent(subscribedToStreamId, string.Empty);

                Assert.AreEqual(1, cache.Items.SelectMany(ccy => ccy.AppliedEvents).Count());

            }
         
        }

        [Test]
        public async Task ShouldSubscribeToAllSubjects()
        {

            using (var publisherSocket = new PublisherSocket())
            {
                publisherSocket.Bind(ToSubscribersEndpoint);

                await Task.Delay(200);

                var createEvent = new Func<string, string, Task>(async (streamId, market) =>
                {
                    var @event = new ChangeCcyPairPrice(streamId, market, 0.0, 0.0, 0.0, 0.0);
                    var message = _eventSerializer.ToProducerMessage(@event);

                    var eventId = new EventId()
                    {
                        EventStream = streamId,
                        Subject = string.IsNullOrEmpty(market) ? streamId : $"{streamId}.{market}",
                        Timestamp = DateTime.Now.Ticks,
                        Version = 0
                    };

                    publisherSocket.SendMoreFrame(message.Subject)
                                   .SendMoreFrame(_serializer.Serialize(eventId))
                                   .SendFrame(_serializer.Serialize(message));

                    await Task.Delay(200);

                });

                var euroDol = "EUR/USD";
                var harmony = "Harmony";

                var euroGbp = "EUR/GBP";
                var fxconnect = "FxConnect";

                var cacheConfigurationEuroDol = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
                {
                    Subject = $"{euroDol}",
                    HeartbeatDelay = TimeSpan.FromSeconds(1),
                    HeartbeatTimeout = TimeSpan.FromSeconds(1),
                };

                var cacheConfigurationAll = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
                {
                    Subject = string.Empty,
                    HeartbeatDelay = TimeSpan.FromSeconds(1),
                    HeartbeatTimeout = TimeSpan.FromSeconds(1),
                };

                var cacheEuroDol = new DynamicCache<string, CurrencyPair>(cacheConfigurationEuroDol, LoggerForTests<DynamicCache<string, CurrencyPair>>.Default(), _eventSerializer);
                var cacheAll = new DynamicCache<string, CurrencyPair>(cacheConfigurationAll, LoggerForTests<DynamicCache<string, CurrencyPair>>.Default(), _eventSerializer);

                await cacheEuroDol.Run();
                await cacheAll.Run();

                await WaitForCachesToCaughtUp(cacheEuroDol, cacheAll);

                Assert.AreEqual(DynamicCacheState.Connected, cacheEuroDol.CacheState);
                Assert.AreEqual(DynamicCacheState.Connected, cacheAll.CacheState);

                await createEvent(euroDol, harmony);

                Assert.AreEqual(1, cacheEuroDol.Items.SelectMany(ccy => ccy.AppliedEvents).Count());
                Assert.AreEqual(1, cacheAll.Items.SelectMany(ccy => ccy.AppliedEvents).Count());

                await createEvent(euroDol, fxconnect);

                await Task.Delay(500);

                Assert.AreEqual(2, cacheEuroDol.Items.SelectMany(ccy=>ccy.AppliedEvents).Count());
                Assert.AreEqual(2, cacheAll.Items.SelectMany(ccy => ccy.AppliedEvents).Count());

                await createEvent(euroDol, string.Empty);

                Assert.AreEqual(3, cacheEuroDol.Items.SelectMany(ccy => ccy.AppliedEvents).Count());
                Assert.AreEqual(3, cacheAll.Items.SelectMany(ccy => ccy.AppliedEvents).Count());

                await createEvent(euroGbp, string.Empty);

                Assert.AreEqual(3, cacheEuroDol.Items.SelectMany(ccy => ccy.AppliedEvents).Count());
                Assert.AreEqual(4, cacheAll.Items.SelectMany(ccy => ccy.AppliedEvents).Count());

                await createEvent(euroGbp, harmony);

                Assert.AreEqual(3, cacheEuroDol.Items.SelectMany(ccy => ccy.AppliedEvents).Count());
                Assert.AreEqual(5, cacheAll.Items.SelectMany(ccy => ccy.AppliedEvents).Count());

                await createEvent(euroGbp, fxconnect);

                Assert.AreEqual(3, cacheEuroDol.Items.SelectMany(ccy => ccy.AppliedEvents).Count());
                Assert.AreEqual(6, cacheAll.Items.SelectMany(ccy => ccy.AppliedEvents).Count());


                await cacheEuroDol.Destroy();
                await cacheAll.Destroy();

             
            }

        }


        [Test]
        public async Task ShouldRetrieveStateOfTheWorld()
        {

            var topic = "EUR/USD";


            for (var i = 0; i < 49; i++)
            {
                var next = Next(topic);
                var message = _eventSerializer.ToProducerMessage(next);
                await _eventCache.AppendToStream(next.Subject, _serializer.Serialize(message));
            }

            var lastPriceEvent = Next(topic);
            var lastMessage = _eventSerializer.ToProducerMessage(lastPriceEvent);
            await _eventCache.AppendToStream(lastPriceEvent.Subject, _serializer.Serialize(lastMessage));

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, LoggerForTests<DynamicCache<string, CurrencyPair>>.Default(), _eventSerializer);

            await cache.Run();

            await WaitForCachesToCaughtUp(cache);

            var ccyPairs = cache.Items
                                .ToList();

            Assert.AreEqual(1, ccyPairs.Count);

            var euroDol = ccyPairs.First();

            Assert.AreEqual(50, euroDol.AppliedEvents.Count());

            Assert.AreEqual(lastPriceEvent.Ask, euroDol.Ask);
            Assert.AreEqual(lastPriceEvent.Bid, euroDol.Bid);
            Assert.AreEqual(lastPriceEvent.Mid, euroDol.Mid);
            Assert.AreEqual(lastPriceEvent.Spread, euroDol.Spread);

            await cache.Destroy();
        }



        [Test]
        public void ShouldSerializeEventSubject()
        {

            var changeCcyPairState = new ChangeCcyPairState("test", "FxConnect", CcyPairState.Active);

            var subject = _eventSerializer.GetSubject(changeCcyPairState);
            Assert.AreEqual("test.Active.FxConnect", subject);

            changeCcyPairState = new ChangeCcyPairState("test", null, CcyPairState.Passive);

            subject = _eventSerializer.GetSubject(changeCcyPairState);
            Assert.AreEqual("test.Passive.*", subject);

            var changeCcyPairPrice = new ChangeCcyPairPrice(
                 ccyPairId: "test",
                 market: "market",
                 ask: 0.1,
                 bid: 0.1,
                 mid: 0.1,
                 spread: 0.02
             );

            subject = _eventSerializer.GetSubject(changeCcyPairPrice);
            Assert.AreEqual("test.market", subject);


        }

        [Test]
        public async Task ShouldApplyMultipleEvents()
        {

            using (var publisherSocket = new PublisherSocket())
            {
                publisherSocket.Bind(ToSubscribersEndpoint);

                var streamId = "EUR/USD";

                var createEvent = new Func<IEvent<String, CurrencyPair>, Task>(async (@event) =>
                 {
                     var message = _eventSerializer.ToProducerMessage(@event);

                     var eventId = new EventId()
                     {
                         EventStream = @event.EventStreamId,
                         Subject = @event.Subject,
                         Timestamp = DateTime.Now.Ticks,
                         Version = 0
                     };

                     publisherSocket.SendMoreFrame(message.Subject)
                                    .SendMoreFrame(_serializer.Serialize(eventId))
                                    .SendFrame(_serializer.Serialize(message));

                     await Task.Delay(200);

                 });

                var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
                {
                    Subject = string.Empty,
                    HeartbeatDelay = TimeSpan.FromSeconds(1),
                    HeartbeatTimeout = TimeSpan.FromSeconds(1),
                };

                var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, LoggerForTests<DynamicCache<string, CurrencyPair>>.Default(), _eventSerializer);

                await cache.Run();

                await Task.Delay(2000);

                var priceEvent = new ChangeCcyPairPrice(streamId, "FxConnect", 0.0, 0.0, 0.0, 0.0);

                await createEvent(priceEvent);

                var changeStateClose = new ChangeCcyPairState(streamId, "FxConnect", CcyPairState.Passive);

                await createEvent(changeStateClose);

                Assert.AreEqual(1, cache.Items.Count());
                Assert.AreEqual(CcyPairState.Passive, cache.Items.First().State);
                Assert.AreEqual(2, cache.Items.SelectMany(ccy => ccy.AppliedEvents).Count());

                await cache.Destroy();

            }

        }


        [Test]
        public void ShouldApplyEvent()
        {
            var ccyPair = new CurrencyPair()
            {
                Ask = 0.1,
                Bid = 0.1,
                Mid = 0.1,
                Spread = 0.02,
                State = CcyPairState.Active,
                Id = "EUR/USD"
            };

            var changeStateClose = new ChangeCcyPairState("EUR/USD", string.Empty, CcyPairState.Passive);

            ccyPair.Apply(changeStateClose);

            Assert.AreEqual(CcyPairState.Passive, ccyPair.State);
            Assert.AreEqual(1, ccyPair.AppliedEvents.Count());

            var changeStateOpen = new ChangeCcyPairState("EUR/USD", string.Empty, CcyPairState.Active);

            ccyPair.Apply(changeStateOpen as IEvent);

            Assert.AreEqual(CcyPairState.Active, ccyPair.State);
            Assert.AreEqual(2, ccyPair.AppliedEvents.Count());
        }
    }
}
