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
using ZeroMQPlayground.DynamicData.Shared;

namespace ZeroMQPlayground.DynamicData
{
   [TestFixture]
    public abstract class TestDynamicDataE2E_Base
    {
        public readonly string ToPublishersEndpoint = "tcp://localhost:8080";
        public readonly string ToSubscribersEndpoint = "tcp://localhost:8181";
        public readonly string HeartbeatEndpoint = "tcp://localhost:8282";
        public readonly string StateOfTheWorldEndpoint = "tcp://localhost:8383";

        protected List<IActor> _actors = new List<IActor>();
        protected InMemoryEventIdProvider _eventIdProvider;
        protected JsonNetSerializer _serializer;
        protected EventSerializer _eventSerializer;
        protected InMemoryEventCache _eventCache;

        [TearDown]
        public async Task TearDown()
        {

            await Task.WhenAll(_actors.Where(actor => actor.State != ActorState.Destroyed)
                                      .Select(async actor => await actor.Destroy()));


            NetMQConfig.Cleanup(false);

         

        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _eventIdProvider = new InMemoryEventIdProvider();
            _serializer = new JsonNetSerializer();
            _eventSerializer = new EventSerializer(_serializer);
            _eventCache = new InMemoryEventCache(_eventIdProvider, _eventSerializer);

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

        public DynamicCache<string, CurrencyPair> GetCache(IDynamicCacheConfiguration configuration)
        {
            var cache = new DynamicCache<string, CurrencyPair>(configuration, _eventSerializer);

            _actors.Add(cache);

            return cache;
        }

        public Market GetMarket(string marketName, IProducerConfiguration configuration, TimeSpan priceGenerationDelay)
        {
            var market = new Market(marketName, configuration, _eventSerializer, priceGenerationDelay);

            _actors.Add(market);

            return market;
        }

        public IActor GetBrokerageService(IBrokerageServiceConfiguration configuration)
        {
            var router = new BrokerageService(configuration, _eventCache, _serializer);
            _actors.Add(router);

            return router;
        }




    }
}
