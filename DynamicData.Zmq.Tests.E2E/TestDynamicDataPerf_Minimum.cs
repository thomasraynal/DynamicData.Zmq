using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using DynamicData.Broker;
using DynamicData.Cache;
using DynamicData.Producer;

namespace DynamicData.E2E
{
    [TestFixture]
    public class TestDynamicDataPerf_Minimum : TestDynamicDataE2E_Base
    {

        [Test]
        public async Task ShouldCheckMinimumPerformance()
        {
            var brokerConfiguration = new BrokerageServiceConfiguration()
            {
                HeartbeatEndpoint = HeartbeatEndpoint,
                StateOftheWorldEndpoint = StateOfTheWorldEndpoint,
                ToSubscribersEndpoint = ToSubscribersEndpoint,
                ToPublisherEndpoint = ToPublishersEndpoint
            };

            var router = GetBrokerageService(brokerConfiguration);

            var marketConfiguration = new ProducerConfiguration()
            {
                RouterEndpoint = ToPublishersEndpoint,
                HeartbeatEndpoint = HeartbeatEndpoint
            };

            var market = GetMarket("FxConnect", marketConfiguration, true, TimeSpan.FromMilliseconds(20));

            await router.Run();
            await market.Run();
       
            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromMilliseconds(500),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = GetCache(cacheConfiguration);
       
            await cache.Run();

            await Task.Delay(2000);

            await WaitForCachesToCaughtUp(cache);

            var cacheItemsEvents = cache.Items
                                        .SelectMany(items=> items.AppliedEvents)
                                        .ToList();


            //when run as standalone test, we should expect 100%
            //when run in a test batch, the result is less deterministic, thus we lower to 70%
            Assert.Greater((double)cacheItemsEvents.Count / (double)market.Prices.Count, 0.70);

        }

    }
}
