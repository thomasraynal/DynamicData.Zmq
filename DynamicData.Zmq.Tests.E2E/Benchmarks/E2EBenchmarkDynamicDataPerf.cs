using NetMQ;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DynamicData.Zmq.Broker;
using DynamicData.Zmq.Cache;
using DynamicData.Zmq.Default;
using DynamicData.Zmq.Demo;
using DynamicData.Zmq.Dto;
using DynamicData.Zmq.Event;
using DynamicData.Zmq.EventCache;
using DynamicData.Zmq.Producer;
using DynamicData.Zmq.Shared;
using BenchmarkDotNet.Attributes;
using System.Threading;
using BenchmarkDotNet.Running;

namespace DynamicData.Tests.E2E.Benchmarks
{
    [MemoryDiagnoser]
    [TestFixture]
    public class Perfbenchmark
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

        [Params(10_000)]
        public int N = 10_000;
        [Params(true,false)]
        public bool UseEventBatch = true;

        private Market _market1;
        private DynamicCache<string, CurrencyPair> _cache;

        [IterationCleanup]
        [OneTimeTearDown]
        public void TearDown()
        {

             Task.WhenAll(_actors.Where(actor => actor.State != ActorState.Destroyed)
                                      .Select(async actor => await actor.Destroy())).Wait();

            NetMQConfig.Cleanup(false);

        }

        [IterationSetup]
        [OneTimeSetUp]
        public void SetUp()
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

                return settings;
            };

            var brokerConfiguration = new BrokerageServiceConfiguration()
            {
                HeartbeatEndpoint = HeartbeatEndpoint,
                StateOfTheWorldEndpoint = StateOfTheWorldEndpoint,
                ToSubscribersEndpoint = ToSubscribersEndpoint,
                ToPublisherEndpoint = ToPublishersEndpoint
            };

            var router = GetBrokerageService(brokerConfiguration);

            var marketConfigurationFxConnect = new MarketConfiguration("FxConnect")
            {
                IsAutoGen = false,
                BrokerEndpoint = ToPublishersEndpoint,
                HeartbeatEndpoint = HeartbeatEndpoint,
                HeartbeatDelay = TimeSpan.FromSeconds(5),
                HeartbeatTimeout = TimeSpan.FromSeconds(5)
            };

            _market1 = GetMarket(marketConfigurationFxConnect);

            router.Run().Wait();

            Task.Delay(1000).Wait();

            _market1.Run().Wait();

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(5),
                HeartbeatTimeout = TimeSpan.FromSeconds(5),
                ZmqHighWatermark = 1000,
                UseEventBatching = UseEventBatch,
                DoStoreEvents = false
            };

            _cache = GetCache(cacheConfiguration);

            _cache.Run().Wait();

            WaitForCachesToCaughtUp(_cache).Wait();
        }


        public DynamicCache<string, CurrencyPair> GetCache(IDynamicCacheConfiguration configuration)
        {
            var cache = new DynamicCache<string, CurrencyPair>(configuration, LoggerForTests<DynamicCache<string, CurrencyPair>>.Default(), _eventSerializer);

            _actors.Add(cache);

            return cache;
        }

        public Market GetMarket(MarketConfiguration configuration)
        {
            var market = new Market(configuration, LoggerForTests<Market>.Default(), _eventSerializer);

            _actors.Add(market);

            return market;
        }

        public IActor GetBrokerageService(IBrokerageServiceConfiguration configuration)
        {
            var router = new BrokerageService(configuration, LoggerForTests<BrokerageService>.Default(), _eventCache, _serializer);
            _actors.Add(router);

            return router;
        }

        public async Task WaitForCachesToCaughtUp(params DynamicCache<string, CurrencyPair>[] caches)
        {
            while (!caches.All(c => !c.IsCaughtingUp))
            {
                await Task.Delay(1000);
            }
        }

       // [Test]
        [Benchmark]
        public void BenchmarkPerformance()
        {
            
            Dictionary<string,ChangeCcyPairPrice> last = new Dictionary<string, ChangeCcyPairPrice>();

            for (var i = 0; i < N; i++)
            {
                var ev = _market1.PublishNext();
                last[ev.EventStreamId] = ev;
            }

            while (_cache.Items.Count() ==0 || _cache.Items.Any(item =>
            {
                return item.Mid != last[item.Id].Mid;
            }))
            {
                Thread.Sleep(1);
            }
        }

        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<Perfbenchmark>();
        }
    }
}
