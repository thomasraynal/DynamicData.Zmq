using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using DynamicData.Zmq.Broker;
using DynamicData.Zmq.Cache;
using DynamicData.Zmq.Demo;
using DynamicData.Zmq.Producer;

namespace DynamicData.Tests.E2E
{
    [TestFixture]
    public class TestDynamicDataE2E_SubscribeToEventFeed : TestDynamicDataE2E_Base
    {

        [Test]
        public async Task ShouldSubscribeToEventFeed()
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
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var marketConfigurationHarmony = new MarketConfiguration("Harmony")
            {
                BrokerEndpoint = ToPublishersEndpoint,
                HeartbeatEndpoint = HeartbeatEndpoint,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var market1 = GetMarket(marketConfigurationFxConnect);
            var market2 = GetMarket(marketConfigurationHarmony);

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = GetCache(cacheConfiguration);

            await router.Run();
            await market1.Run();
            await market2.Run();

            //create an event cache
            await Task.Delay(2000);

            var routerEventCacheItems = await _eventCache.GetStreamBySubject(string.Empty);

            Assert.Greater(routerEventCacheItems.Count(), 0);

            var counter = 0;

            var cleanup = cache.OnItemChanged
                               .Connect()
                               .Subscribe(changes =>
                               {
                                   counter += changes.Count;
                               });

            await cache.Run();

            await Task.Delay(1000);

            await WaitForCachesToCaughtUp(cache);

            var eventCacheItems = cache.Items.SelectMany(item => item.AppliedEvents).ToList();

            Assert.AreEqual(eventCacheItems.Count(), counter);

            var markets = cache.Items
                                    .SelectMany(item => item.AppliedEvents)
                                    .Cast<ChangeCcyPairPrice>()
                                    .Select(ev => ev.Market)
                                    .Distinct();

            //fxconnext & harmony
            Assert.AreEqual(2, markets.Count());

            cleanup.Dispose();

       
        }

    }
}
