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
        private string ToPublishersEndpoint = "tcp://localhost:8080";
        private string ToSubscribersEndpoint = "tcp://localhost:8181";
        private string HeartbeatEndpoint = "tcp://localhost:8282";
        private string StateOfTheWorldEndpoint = "tcp://localhost:8383";

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

            var market = new Market("FxConnect", marketConfiguration, eventSerializer, TimeSpan.FromMilliseconds(10));
      
            await router.Run();

            await market.Run();
       
            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(10),
                HeartbeatTimeout = TimeSpan.FromSeconds(2)
            };

            var cache = new DynamicCache<string, CurrencyPair>(cacheConfiguration, eventSerializer);
       
            await cache.Run();

            //fire a little less than 300 events
            await Task.Delay(3000);

            await Task.WhenAll(new[] { router.Destroy(), market.Destroy(), cache.Destroy() });

            var cacheItems = cache.GetItems().ToList();
            var cacheItemsEvents = cacheItems.SelectMany(items=> items.AppliedEvents).ToList();

            //At least 3/4 events should be processed
            Assert.Greater((double)cacheItemsEvents.Count / (double)market.Prices.Count, 0.75);


        }

    }
}
