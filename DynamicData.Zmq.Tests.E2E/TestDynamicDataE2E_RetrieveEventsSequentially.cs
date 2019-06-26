﻿using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using ZeroMQPlayground.DynamicData.Broker;
using ZeroMQPlayground.DynamicData.Cache;
using ZeroMQPlayground.DynamicData.Demo;
using ZeroMQPlayground.DynamicData.Event;
using ZeroMQPlayground.DynamicData.Producer;

namespace ZeroMQPlayground.DynamicData.E2E
{
    [TestFixture]
    public class TestDynamicDataE2E_RetrieveEventsSequentially : TestDynamicDataE2E_Base
    {

        [Test]
        public async Task ShouldRetrievedEventsSequentially()
        {
    
            var brokerConfiguration = new BrokerageServiceConfiguration()
            {
                HeartbeatEndpoint = HeartbeatEndpoint,
                StateOftheWorldEndpoint = StateOfTheWorldEndpoint,
                ToSubscribersEndpoint = ToSubscribersEndpoint,
                ToPublisherEndpoint = ToPublishersEndpoint
            };

            var marketConfiguration = new ProducerConfiguration()
            {
                RouterEndpoint = ToPublishersEndpoint,
                HearbeatEndpoint = HeartbeatEndpoint
            };

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(10),
                HeartbeatTimeout = TimeSpan.FromSeconds(2)
            };

            var router = GetBrokerageService(brokerConfiguration);

            var market1 = GetMarket("FxConnect", marketConfiguration, TimeSpan.FromMilliseconds(1000));
            var market2 = GetMarket("Harmony", marketConfiguration, TimeSpan.FromMilliseconds(1000));

            var cache = GetCache(cacheConfiguration);
            var cacheProof = GetCache(cacheConfiguration);

            await router.Run();

            await market1.Run();
            await market2.Run();

            await cache.Run();
            await cacheProof.Run();

            await Task.Delay(1000);

            await WaitForCachesToCaughtUp(cache, cacheProof);

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

         
        }

    }
}