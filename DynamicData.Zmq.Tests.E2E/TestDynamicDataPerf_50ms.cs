using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using DynamicData.Zmq.Broker;
using DynamicData.Zmq.Cache;
using DynamicData.Zmq.Demo;
using DynamicData.Zmq.Event;
using DynamicData.Zmq.Producer;

namespace DynamicData.Tests.E2E
{
    [TestFixture]
    public class TestDynamicDataPerf_50ms: TestDynamicDataE2E_Base
    {

        [Test]
        public async Task ShouldCheckMaxPerformance()
        {

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
                BrokerEndpoint = ToPublishersEndpoint,
                HeartbeatEndpoint = HeartbeatEndpoint,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1),
                PriceGenerationDelay = TimeSpan.FromMilliseconds(50)
            };

            var marketConfigurationHarmony = new MarketConfiguration("Harmony")
            {
                BrokerEndpoint = ToPublishersEndpoint,
                HeartbeatEndpoint = HeartbeatEndpoint,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1),
                PriceGenerationDelay = TimeSpan.FromMilliseconds(50)
            };

            var market1 = GetMarket(marketConfigurationFxConnect);
            var market2 = GetMarket(marketConfigurationHarmony);

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

            var cache = GetCache(cacheConfiguration);

            await cache.Run();

            await Task.Delay(2000);

            await WaitForCachesToCaughtUp(cache);

            var cacheEvents = cache.Items
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


    }
}
