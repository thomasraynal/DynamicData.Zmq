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
    public class TestDynamicDataPerf
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
        public async Task ShouldCheckMaxPerformance()
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

            var market1 = new Market("FxConnect", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(30));
            var market2 = new Market("Harmony", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(30));

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

            await cache.Run();

            await Task.Delay(5000);

            await Task.WhenAll(new[] {
                router.Destroy(),
                market1.Destroy(),
                market2.Destroy(),
                cache.Destroy() });

            var cacheEvents = cache.GetItems()
                       .SelectMany(item => item.AppliedEvents)
                       .Cast<IEvent<string, CurrencyPair>>()
                       .GroupBy(ev => ev.EventStreamId)
                       .ToList();


            Assert.Greater(cacheEvents.Count, 0);

            foreach (var grp in cacheEvents)
            {
                var index = 0;

                foreach (var ev in grp)
                {
                    Assert.AreEqual(index++, ev.Version);
                }
            }

        }

        [Test]
        public async Task ShouldCheckMinimumPerformance()
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

            var market = new Market("FxConnect", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(20));
      
            await router.Run();

            await market.Run();
       
            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromMilliseconds(500),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, eventSerializer);
       
            await cache.Run();

            await Task.Delay(3000);

            var cacheItems = cache.GetItems().ToList();
            var cacheItemsEvents = cacheItems.SelectMany(items=> items.AppliedEvents).ToList();

            Assert.Greater((double)cacheItemsEvents.Count / (double)market.Prices.Count, 0.80);


            await Task.WhenAll(new[] { router.Destroy(), market.Destroy(), cache.Destroy() });


        }

    }
}
