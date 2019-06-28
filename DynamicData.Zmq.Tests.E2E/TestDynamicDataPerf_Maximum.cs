﻿using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using DynamicData.Broker;
using DynamicData.Cache;
using DynamicData.Demo;
using DynamicData.Event;
using DynamicData.Producer;

namespace DynamicData.Tests.E2E
{
    [TestFixture]
    public class TestDynamicDataPerf_100ms: TestDynamicDataE2E_Base
    {

        [Test]
        public async Task ShouldCheckMaxPerformance()
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
                BrokerEndpoint = ToPublishersEndpoint,
                HeartbeatEndpoint = HeartbeatEndpoint,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var market1 = GetMarket("FxConnect", marketConfiguration, true, TimeSpan.FromMilliseconds(100));
            var market2 = GetMarket("Harmony", marketConfiguration, true, TimeSpan.FromMilliseconds(100));

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
