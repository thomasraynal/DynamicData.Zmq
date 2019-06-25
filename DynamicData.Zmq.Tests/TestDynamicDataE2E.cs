using NetMQ;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroMQPlayground.DynamicData.Broker;
using ZeroMQPlayground.DynamicData.Cache;
using ZeroMQPlayground.DynamicData.Default;
using ZeroMQPlayground.DynamicData.Demo;
using ZeroMQPlayground.DynamicData.Dto;
using ZeroMQPlayground.DynamicData.Event;
using ZeroMQPlayground.DynamicData.EventCache;
using ZeroMQPlayground.DynamicData.Producer;

namespace ZeroMQPlayground.DynamicData
{
    //given the huge number of tasks running for each E2E test, a substantial timer is necessary for the system to change state.
    [TestFixture]
    public class TestDynamicDataE2E
    {
        private readonly string ToPublishersEndpoint = "tcp://localhost:8080";
        private readonly string ToSubscribersEndpoint = "tcp://localhost:8181";
        private readonly string HeartbeatEndpoint = "tcp://localhost:8282";
        private readonly string StateOfTheWorldEndpoint = "tcp://localhost:8383";

        [TearDown]
        public void TearDown()
        {
            NetMQConfig.Cleanup(false);
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
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
        public async Task ShouldConnectAndDisconnectFromBrokerGracefully()
        {
            var eventIdProvider = new InMemoryEventIdProvider();
            var serializer = new JsonNetSerializer();
            var eventSerializer = new EventSerializer(serializer);

            var eventCache = new InMemoryEventCache(eventIdProvider, eventSerializer);

            var brokerConfiguration = new BrokerageServiceConfiguration()
            {
                HeartbeatEndpoint = HeartbeatEndpoint,
                StateOftheWorldEndpoint = StateOfTheWorldEndpoint,
                ToSubscribersEndpoint = ToSubscribersEndpoint,
                ToPublisherEndpoint = ToPublishersEndpoint
            };

            var router = new BrokerageService(brokerConfiguration, eventCache, serializer);

            var marketConfiguration = new ProducerConfiguration()
            {
                RouterEndpoint = ToPublishersEndpoint,
                HearbeatEndpoint = HeartbeatEndpoint,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var market = new Market("FxConnect", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(750));

            await market.Run();

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, eventSerializer);

            var cacheStates = new List<DynamicCacheState>();

            var stateObservable = cache.OnStateChanged()
                                       .Subscribe(state =>
                                       {
                                           cacheStates.Add(state);
                                       });

            await cache.Run();

            await Task.Delay(2000);

            Assert.AreEqual(DynamicCacheState.NotConnected, cache.CacheState);
            Assert.AreEqual(ProducerState.NotConnected, market.ProducerState);

            await router.Run();

            await Task.Delay(2000);

            Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);
            Assert.AreEqual(ProducerState.Connected, market.ProducerState);

            await router.Destroy();

            await Task.Delay(2000);

            Assert.AreEqual(DynamicCacheState.Disconnected, cache.CacheState);
            Assert.AreEqual(ProducerState.Disconnected, market.ProducerState);

            router = new BrokerageService(brokerConfiguration, eventCache, serializer);
            await router.Run();

            await Task.Delay(2000);

            Assert.AreEqual(5, cacheStates.Count);
            Assert.AreEqual(DynamicCacheState.NotConnected, cacheStates.ElementAt(0));
            Assert.AreEqual(DynamicCacheState.Connected, cacheStates.ElementAt(1));
            Assert.AreEqual(DynamicCacheState.Disconnected, cacheStates.ElementAt(2));
            Assert.AreEqual(DynamicCacheState.Reconnected, cacheStates.ElementAt(3));
            Assert.AreEqual(DynamicCacheState.Connected, cacheStates.ElementAt(4));

            Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);
            Assert.AreEqual(ProducerState.Connected, market.ProducerState);


            await Task.WhenAll(new[] { router.Destroy(), market.Destroy(), cache.Destroy() });


        }


        [Test]
        public async Task ShouldHandleDisconnectAndCacheRebuild()
        {
            var eventIdProvider = new InMemoryEventIdProvider();
            var serializer = new JsonNetSerializer();
            var eventSerializer = new EventSerializer(serializer);

            var eventCache = new InMemoryEventCache(eventIdProvider, eventSerializer);

            var brokerConfiguration = new BrokerageServiceConfiguration()
            {
                HeartbeatEndpoint = HeartbeatEndpoint,
                StateOftheWorldEndpoint = StateOfTheWorldEndpoint,
                ToSubscribersEndpoint = ToSubscribersEndpoint,
                ToPublisherEndpoint = ToPublishersEndpoint
            };

            var router = new BrokerageService(brokerConfiguration, eventCache, serializer);

            var marketConfiguration = new ProducerConfiguration()
            {
                RouterEndpoint = ToPublishersEndpoint,
                HearbeatEndpoint = HeartbeatEndpoint,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var market1 = new Market("FxConnect", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(750));
            var market2 = new Market("Harmony", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(750));

            await router.Run();

            await Task.Delay(1000);

            await market1.Run();
            await market2.Run();

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, eventSerializer);
            var cacheProof = new DynamicCache<string, CurrencyPair>(cacheConfiguration, eventSerializer);

            await cache.Run();
            await cacheProof.Run();

            Assert.AreEqual(DynamicCacheState.NotConnected, cache.CacheState);
            Assert.AreEqual(DynamicCacheState.NotConnected, cacheProof.CacheState);

            await Task.Delay(2000);

            Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);
            Assert.AreEqual(DynamicCacheState.Connected, cacheProof.CacheState);

            await Task.Delay(2000);

            var cacheEvents = cache.GetItems().SelectMany(item => item.AppliedEvents).ToList();
            var cacheProofEvents = cacheProof.GetItems().SelectMany(item => item.AppliedEvents).ToList();

            Assert.AreEqual(cacheEvents.Count(), cacheProofEvents.Count());

            await router.Destroy();

            await Task.Delay(2000);

            Assert.AreEqual(DynamicCacheState.Disconnected, cache.CacheState);
            Assert.AreEqual(DynamicCacheState.Disconnected, cacheProof.CacheState);

            router = new BrokerageService(brokerConfiguration, eventCache, serializer);

            await router.Run();

            await Task.Delay(2000);

            Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);
            Assert.AreEqual(DynamicCacheState.Connected, cacheProof.CacheState);

            await router.Destroy();

            var cacheCCyPair = cache.GetItems().ToList();
            var cacheProofCcyPair = cacheProof.GetItems().ToList();

            Assert.AreEqual(cacheCCyPair.Count(), cacheProofCcyPair.Count());
            Assert.AreEqual(cacheCCyPair.Count(), cacheProofCcyPair.Count());

            foreach (var ccyPair in cacheCCyPair)
            {
                var proof = cacheProofCcyPair.First(ccy => ccy.Id == ccyPair.Id);

                Assert.AreEqual(ccyPair.Ask, proof.Ask);
                Assert.AreEqual(ccyPair.Bid, proof.Bid);
                Assert.AreEqual(ccyPair.Mid, proof.Mid);
                Assert.AreEqual(ccyPair.Spread, proof.Spread);
            }

            var brokerCacheEvents = (await eventCache.GetStreamBySubject(cacheConfiguration.Subject)).ToList();
            cacheEvents = cacheCCyPair.SelectMany(item => item.AppliedEvents).ToList();
            cacheProofEvents = cacheProofCcyPair.SelectMany(item => item.AppliedEvents).ToList();

            Assert.AreEqual(cacheEvents.Count(), cacheProofEvents.Count());
            Assert.AreEqual(cacheEvents.Count(), cacheProofEvents.Count());
            Assert.AreEqual(cacheEvents.Count(), cacheProofEvents.Count());

            await Task.WhenAll(new[] {market1.Destroy(), market2.Destroy(), cache.Destroy(), cacheProof.Destroy() });

        }

        [Test]
        public async Task ShouldSubscribeToSubject()
        {
            //todo .NET COre MVC implem
            var eventIdProvider = new InMemoryEventIdProvider();
            var serializer = new JsonNetSerializer();
            var eventSerializer = new EventSerializer(serializer);

            var eventCache = new InMemoryEventCache(eventIdProvider, eventSerializer);

            var brokerConfiguration = new BrokerageServiceConfiguration()
            {
                HeartbeatEndpoint = HeartbeatEndpoint,
                StateOftheWorldEndpoint = StateOfTheWorldEndpoint,
                ToSubscribersEndpoint = ToSubscribersEndpoint,
                ToPublisherEndpoint = ToPublishersEndpoint
            };

            var router = new BrokerageService(brokerConfiguration, eventCache, serializer);

            var marketConfiguration = new ProducerConfiguration()
            {
                RouterEndpoint = ToPublishersEndpoint,
                HearbeatEndpoint = HeartbeatEndpoint
            };

            var market1 = new Market("FxConnect", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(750));
            var market2 = new Market("Harmony", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(750));

            await router.Run();

            await market1.Run();
            await market2.Run();

            var cacheConfigurationEuroDol = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = "EUR/USD"
            };

            var cacheConfigurationEuroDolFxConnect = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = "EUR/USD.FxConnect"
            };

            var cacheEuroDol = new DynamicCache<string, CurrencyPair>(cacheConfigurationEuroDol, eventSerializer);
            var cacheEuroDolFxConnect = new DynamicCache<string, CurrencyPair>(cacheConfigurationEuroDolFxConnect, eventSerializer);

            await cacheEuroDol.Run();
            await cacheEuroDolFxConnect.Run();

            //wait for a substential event stream
            await Task.Delay(7000);

            var routerEventCacheItems = await eventCache.GetStreamBySubject(string.Empty);

            Assert.Greater(routerEventCacheItems.Count(), 0);


            var ccyPairsCacheEuroDol = cacheEuroDol.GetItems()
                                                   .SelectMany(item => item.AppliedEvents)
                                                   .Select(item => item.Subject)
                                                   .Distinct();

            // EUR/USD.FxConnect & EUR/USD.Harmony
            Assert.AreEqual(2, ccyPairsCacheEuroDol.Count());
            Assert.IsTrue(ccyPairsCacheEuroDol.All(subject => subject.EndsWith("FxConnect") || subject.EndsWith("Harmony")));
            Assert.IsTrue(ccyPairsCacheEuroDol.All(subject => subject.StartsWith(cacheConfigurationEuroDol.Subject)));

            var ccyPairsCacheEuroDolFxConnect = cacheEuroDolFxConnect.GetItems()
                                                                     .SelectMany(item => item.AppliedEvents)
                                                                     .Select(item => item.Subject)
                                                                     .Distinct();

            // EUR/USD.FxConnect
            Assert.AreEqual(1, ccyPairsCacheEuroDolFxConnect.Count());
            Assert.AreEqual(cacheConfigurationEuroDolFxConnect.Subject, ccyPairsCacheEuroDolFxConnect.First());


            await Task.WhenAll(new[] { router.Destroy(), market1.Destroy(), market2.Destroy(), cacheEuroDol.Destroy(), cacheEuroDolFxConnect.Destroy() });
        }

        [Test]
        public async Task ShouldSubscribeToEventFeed()
        {
            var eventIdProvider = new InMemoryEventIdProvider();
            var serializer = new JsonNetSerializer();
            var eventSerializer = new EventSerializer(serializer);
            var eventCache = new InMemoryEventCache(eventIdProvider, eventSerializer);

            var brokerConfiguration = new BrokerageServiceConfiguration()
            {
                HeartbeatEndpoint = HeartbeatEndpoint,
                StateOftheWorldEndpoint = StateOfTheWorldEndpoint,
                ToSubscribersEndpoint = ToSubscribersEndpoint,
                ToPublisherEndpoint = ToPublishersEndpoint
            };

            var router = new BrokerageService(brokerConfiguration, eventCache, serializer);

            var marketConfiguration = new ProducerConfiguration()
            {
                RouterEndpoint = ToPublishersEndpoint,
                HearbeatEndpoint = HeartbeatEndpoint,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var market1 = new Market("FxConnect", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(750));
            var market2 = new Market("Harmony", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(750));

            await router.Run();
            await market1.Run();
            await market2.Run();

            //create an event cache
            await Task.Delay(2000);

            var routerEventCacheItems = await eventCache.GetStreamBySubject(string.Empty);

            Assert.Greater(routerEventCacheItems.Count(), 0);

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, eventSerializer);

            var counter = 0;

            var cleanup = cache.OnItemChanged()
                               .Connect()
                               .Subscribe(changes =>
                               {
                                   counter += changes.Count;
                               });

            await cache.Run();

            await Task.Delay(3000);

            var eventCacheItems = cache.GetItems().SelectMany(item => item.AppliedEvents).ToList();

            Assert.AreEqual(eventCacheItems.Count(), counter);

            var markets = cache.GetItems()
                                    .SelectMany(item => item.AppliedEvents)
                                    .Cast<ChangeCcyPairPrice>()
                                    .Select(ev => ev.Market)
                                    .Distinct();

            //fxconnext & harmony
            Assert.AreEqual(2, markets.Count());

            cleanup.Dispose();

            await Task.WhenAll(new[] { router.Destroy(), market1.Destroy(), market2.Destroy(), cache.Destroy() });

        }

        [Test]
        public async Task ShouldRetrievedEventsSequencialy()
        {
            var eventIdProvider = new InMemoryEventIdProvider();
            var serializer = new JsonNetSerializer();
            var eventSerializer = new EventSerializer(serializer);

            var eventCache = new InMemoryEventCache(eventIdProvider, eventSerializer);

            var brokerConfiguration = new BrokerageServiceConfiguration()
            {
                HeartbeatEndpoint = HeartbeatEndpoint,
                StateOftheWorldEndpoint = StateOfTheWorldEndpoint,
                ToSubscribersEndpoint = ToSubscribersEndpoint,
                ToPublisherEndpoint = ToPublishersEndpoint
            };

            var router = new BrokerageService(brokerConfiguration, eventCache, serializer);

            var marketConfiguration = new ProducerConfiguration()
            {
                RouterEndpoint = ToPublishersEndpoint,
                HearbeatEndpoint = HeartbeatEndpoint
            };

            var market1 = new Market("FxConnect", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(100));
            var market2 = new Market("Harmony", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(100));

            await router.Run();
            await market1.Run();
            await market2.Run();

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(10),
                HeartbeatTimeout = TimeSpan.FromSeconds(2)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, eventSerializer);
            var cacheProof = new DynamicCache<string, CurrencyPair>(cacheConfiguration, eventSerializer);

            await cache.Run();
            await cacheProof.Run();

            await Task.Delay(5000);

            var cacheEvents = cache.GetItems()
                                   .SelectMany(item => item.AppliedEvents)
                                   .Cast<IEvent<string, CurrencyPair>>()
                                   .GroupBy(ev => ev.EventStreamId)
                                   .ToList();

            foreach (var grp in cacheEvents)
            {
                var index = 0;

                foreach (var ev in grp)
                {
                    Assert.AreEqual(index++, ev.Version);
                }
            }


            var cacheProofEvents = cache.GetItems()
                       .SelectMany(item => item.AppliedEvents)
                       .Cast<IEvent<string, CurrencyPair>>()
                       .GroupBy(ev => ev.EventStreamId)
                       .ToList();

            foreach (var grp in cacheProofEvents)
            {
                var index = 0;

                foreach (var ev in grp)
                {
                    Assert.AreEqual(index++, ev.Version);
                }
            }

            await Task.WhenAll(new[] { router.Destroy(), market1.Destroy(), market2.Destroy(), cache.Destroy(), cacheProof.Destroy() });

        }

    }
}
