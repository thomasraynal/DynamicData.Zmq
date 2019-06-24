using NetMQ;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using System;
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
    [TestFixture]
    public class TestDynamicDataE2E
    {
        private string ToPublishersEndpoint = "tcp://localhost:8080";
        private string ToSubscribersEndpoint = "tcp://localhost:8181";
        private string HeartbeatEndpoint = "tcp://localhost:8282";
        private string StateOfTheWorldEndpoint = "tcp://localhost:8383";

        //[TearDown]
        //public void TearDown()
        //{
        //    NetMQConfig.Cleanup(false);
        //}

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
                HeartbeatDelay = TimeSpan.FromSeconds(2),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, eventSerializer);
            var cacheProof = new DynamicCache<string, CurrencyPair>(cacheConfiguration, eventSerializer);

            await cacheProof.Run();
            await cache.Run();

            Assert.AreEqual(DynamicCacheState.None, cache.CacheState);
            Assert.AreEqual(DynamicCacheState.None, cacheProof.CacheState);

            await Task.Delay(4000);

            Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);
            Assert.AreEqual(DynamicCacheState.Connected, cacheProof.CacheState);

            await Task.Delay(3000);

            Assert.AreEqual(cache.GetItems()
                                 .SelectMany(item => item.AppliedEvents)
                                 .Count(), 
                       cacheProof.GetItems()
                                 .SelectMany(item => item.AppliedEvents)
                                 .Count());

            await router.Destroy();

            await Task.Delay(cacheConfiguration.HeartbeatDelay);

            Assert.AreEqual(DynamicCacheState.Disconnected, cache.CacheState);
            Assert.AreEqual(DynamicCacheState.Disconnected, cacheProof.CacheState);

            router = new BrokerageService(brokerConfiguration, eventCache, serializer);

            await router.Run();

            await Task.Delay(TimeSpan.FromSeconds(3));

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
            var cacheEvents = cacheCCyPair.SelectMany(item => item.AppliedEvents).ToList();
            var cacheProofEvents = cacheProofCcyPair.SelectMany(item => item.AppliedEvents).ToList();
      

            Assert.AreEqual(cacheEvents.Count(), cacheProofEvents.Count());
            Assert.AreEqual(cacheEvents.Count(), cacheProofEvents.Count());
            Assert.AreEqual(cacheEvents.Count(), cacheProofEvents.Count());

            await Task.WhenAll(new[] { market1.Destroy(), market2.Destroy(), cache.Destroy()});

        }

        [Test]
        public async Task ShouldSubscribeToSubject()
        {
            //todo .NET COre MVC implem
            var eventIdProvider = new InMemoryEventIdProvider();
            var serializer = new JsonNetSerializer();
            var eventSerializer = new EventSerializer(serializer);
          
            var eventCache = new InMemoryEventCache(eventIdProvider,eventSerializer);

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
            await Task.Delay(5000);

            // Assert.Greater(router.Cache.Count(), 1);

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
                HearbeatEndpoint = HeartbeatEndpoint
            };

            var market1 = new Market("FxConnect", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(750));
            var market2 = new Market("Harmony", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(750));

            await router.Run();
            await market1.Run();
            await market2.Run();

            //create an event cache
            await Task.Delay(2000);

            //Assert.Greater(router.Cache.Count(), 0);

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint);

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, eventSerializer);

            var counter = 0;

            var cleanup = cache.OnItemChanged()
                               .Connect()
                               .Subscribe(_ =>
                               {
                                   counter++;
                               });

            await cache.Run();

            await Task.Delay(2000);

            // Assert.AreEqual(router.Cache.Count(), counter);
            Assert.AreEqual(cache.GetItems().SelectMany(item => item.AppliedEvents).Count(), counter);

            //fxconnext & harmony
            Assert.AreEqual(2, cache.GetItems()
                                    .SelectMany(item => item.AppliedEvents)
                                    .Cast<ChangeCcyPairPrice>()
                                    .Select(ev => ev.Market)
                                    .Distinct()
                                    .Count());


            cleanup.Dispose();

            await Task.WhenAll(new[] { router.Destroy(), market1.Destroy(), market2.Destroy(), cache.Destroy() });

        }

    }
}
