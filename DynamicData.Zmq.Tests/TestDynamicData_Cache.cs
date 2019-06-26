﻿using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQPlayground.DynamicData.Cache;
using ZeroMQPlayground.DynamicData.Default;
using ZeroMQPlayground.DynamicData.Demo;
using ZeroMQPlayground.DynamicData.Dto;
using ZeroMQPlayground.DynamicData.Event;
using ZeroMQPlayground.DynamicData.EventCache;
using ZeroMQPlayground.DynamicData.Producer;

namespace ZeroMQPlayground.DynamicData
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
        public async Task ShouldHandleNewEventsWhileRebuildingTheCache()
        {
            var eventIdProvider = new InMemoryEventIdProvider();
            var eventCache = new InMemoryEventCache(eventIdProvider, _eventSerializer);
            var topic = "EUR/USD";
            var cancel = new CancellationTokenSource();

            var stateOfTheWorldEventCount = 10000;
            var ongoingEventCount = 10;

            var raisedEventDuringCacheBuilding = new List<IEventId>();

            SetupFakeBroker(cancel.Token, eventCache);

            await Task.Delay(500);

            for (var i = 0; i < stateOfTheWorldEventCount; i++)
            {
                var next = Next(topic);
                var message = _eventSerializer.ToProducerMessage(next);
                await eventCache.AppendToStream(next.Subject, _serializer.Serialize(message));
            }

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, _eventSerializer);

            var task = new Thread(() =>
            {
                using (var stateUpdatePublish = new PublisherSocket())
                {
                    stateUpdatePublish.Bind(ToSubscribersEndpoint);

                    for (var i = 0; i < ongoingEventCount; i++)
                    {
                        if (cancel.IsCancellationRequested) return;

                        var next = Next(topic);
                        var message = _eventSerializer.ToProducerMessage(next);
                        var payload = _serializer.Serialize(message);
                        var eventId = eventCache.AppendToStream(next.Subject, payload).Result;

                        raisedEventDuringCacheBuilding.Add(eventId);

                        stateUpdatePublish.SendMoreFrame(next.Subject)
                                          .SendMoreFrame(_serializer.Serialize(eventId))
                                          .SendFrame(payload);


                    }
                }

            });

            await cache.Run();

            task.Start();

            await Task.Delay(2000);

            await DestroyFakeBroker(cancel);

            var ccyPairs = cache.GetItems()
                                .ToList();

            Assert.AreEqual(1, ccyPairs.Count);

            var euroDol = ccyPairs.First();

            Assert.AreEqual(stateOfTheWorldEventCount + ongoingEventCount, euroDol.AppliedEvents.Count());



        }

        [Test]
        public async Task ShouldSwitchToStaledState()
        {
            using (var publisherSocket = new PublisherSocket())
            {
                publisherSocket.Bind(ToSubscribersEndpoint);

                var eventIdProvider = new InMemoryEventIdProvider();
                var eventCache = new InMemoryEventCache(eventIdProvider, _eventSerializer);

                var cancel = new CancellationTokenSource();

                //we handle event loop
                SetupFakeBroker(cancel.Token, eventCache, useEventLoop: false);

                await Task.Delay(2000);

                var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
                {
                    Subject = string.Empty,
                    HeartbeatDelay = TimeSpan.FromSeconds(5),
                    HeartbeatTimeout = TimeSpan.FromSeconds(1),
                    IsStaleTimeout = TimeSpan.FromSeconds(2)
                };

                var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, _eventSerializer);

                await cache.Run();

                await Task.Delay(1000);

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


                await Task.Delay(2000);

                Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);
                Assert.AreEqual(true, cache.IsStaled);

                await DestroyFakeBroker(cancel);

                await Task.Delay(2000);

                Assert.AreEqual(true, cache.IsStaled);

            }
        }


        [Test]
        public async Task ShouldProduceEventsAndConsumeThemViaEventCache()
        {
            var eventIdProvider = new InMemoryEventIdProvider();
            var eventCache = new InMemoryEventCache(eventIdProvider, _eventSerializer);
            var cancel = new CancellationTokenSource();

            SetupFakeBroker(cancel.Token, eventCache);

            await Task.Delay(2000);

            var marketConfiguration = new ProducerConfiguration()
            {
                RouterEndpoint = ToPublishersEndpoint,
                HearbeatEndpoint = HeartbeatEndpoint,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var producer = new Market("TEST", marketConfiguration, _eventSerializer, TimeSpan.FromMilliseconds(1000));
            await producer.Run();

            await Task.Delay(2000);

            var eventCountOnProducer = producer.Prices.Count;
            var eventsOnEventCache = (await eventCache.GetAllStreams())
                .Select(msg => _serializer.Deserialize<ChangeCcyPairPrice>(msg.ProducerMessage.MessageBytes))
                .ToList();

            Assert.Greater(eventCountOnProducer, 0);
            Assert.Greater(eventsOnEventCache.Count, 0);
            Assert.AreEqual(eventCountOnProducer, eventsOnEventCache.Count);

            Assert.IsTrue(eventsOnEventCache.All(ev => producer.Prices.Any(p=> p.Ask == ev.Ask)));

            producer.Publish(new ChangeCcyPairPrice("TEST", "TEST-Market", 0.0, 0.0, 0.0, 0.0));

            await Task.Delay(200);

            var testEvent = await eventCache.GetStream("TEST");

            Assert.AreEqual(1, testEvent.Count());

            await DestroyFakeBroker(cancel);

        }

        [Test]
        public async Task ShouldPerformHeartbeat()
        {
            var eventIdProvider = new InMemoryEventIdProvider();
            var eventCache = new InMemoryEventCache(eventIdProvider, _eventSerializer);

            var cancel = new CancellationTokenSource();

            SetupFakeBroker(cancel.Token, eventCache);

            await Task.Delay(2000);

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, _eventSerializer);

            await cache.Run();

            await Task.Delay(1000);

            Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);

            await DestroyFakeBroker(cancel);
        }

        [Test]
        public void ShouldSubscribeToSubject()
        {
            throw new NotImplementedException();
        }

        [Test]
        public async Task ShouldRetrieveStateOfTheWorld()
        {

            var eventIdProvider = new InMemoryEventIdProvider();
            var eventCache = new InMemoryEventCache(eventIdProvider, _eventSerializer);

            var topic = "EUR/USD";

            var cancel = new CancellationTokenSource();

            SetupFakeBroker(cancel.Token, eventCache);

            await Task.Delay(500);

            for (var i = 0; i < 49; i++)
            {
                var next = Next(topic);
                var message = _eventSerializer.ToProducerMessage(next);
                await eventCache.AppendToStream(next.Subject, _serializer.Serialize(message));
            }

            var lastPriceEvent = Next(topic);
            var lastMessage = _eventSerializer.ToProducerMessage(lastPriceEvent);
            await eventCache.AppendToStream(lastPriceEvent.Subject, _serializer.Serialize(lastMessage));

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, _eventSerializer);

            await cache.Run();

            await Task.Delay(3000);

            var ccyPairs = cache.GetItems()
                                .ToList();

            Assert.AreEqual(1, ccyPairs.Count);

            var euroDol = ccyPairs.First();

            Assert.AreEqual(50, euroDol.AppliedEvents.Count());

            Assert.AreEqual(lastPriceEvent.Ask, euroDol.Ask);
            Assert.AreEqual(lastPriceEvent.Bid, euroDol.Bid);
            Assert.AreEqual(lastPriceEvent.Mid, euroDol.Mid);
            Assert.AreEqual(lastPriceEvent.Spread, euroDol.Spread);

            await DestroyFakeBroker(cancel);
        }



        [Test]
        public void ShouldSerializeEventSubject()
        {
            var serializer = new JsonNetSerializer();
            var eventSerializer = new EventSerializer(serializer);

            var changeCcyPairState = new ChangeCcyPairState()
            {
                EventStreamId = "test",
                State = CcyPairState.Active,
                Market = "FxConnect"
            };

            var subject = eventSerializer.GetSubject(changeCcyPairState);
            Assert.AreEqual("test.Active.FxConnect", subject);

            changeCcyPairState = new ChangeCcyPairState()
            {
                EventStreamId = "test",
                State = CcyPairState.Passive,
            };

            subject = eventSerializer.GetSubject(changeCcyPairState);
            Assert.AreEqual("test.Passive.*", subject);

            var changeCcyPairPrice = new ChangeCcyPairPrice(
                 ccyPairId: "test",
                 market: "market",
                 ask: 0.1,
                 bid: 0.1,
                 mid: 0.1,
                 spread: 0.02
             );

            subject = eventSerializer.GetSubject(changeCcyPairPrice);
            Assert.AreEqual("test.market", subject);


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

            var changeStateClose = new ChangeCcyPairState()
            {
                State = CcyPairState.Passive
            };

            ccyPair.Apply(changeStateClose);

            Assert.AreEqual(CcyPairState.Passive, ccyPair.State);
            Assert.AreEqual(1, ccyPair.AppliedEvents.Count());

            var changeStateOpen = new ChangeCcyPairState()
            {
                State = CcyPairState.Active
            };

            ccyPair.Apply(changeStateOpen as IEvent);

            Assert.AreEqual(CcyPairState.Active, ccyPair.State);
            Assert.AreEqual(2, ccyPair.AppliedEvents.Count());
        }
    }
}