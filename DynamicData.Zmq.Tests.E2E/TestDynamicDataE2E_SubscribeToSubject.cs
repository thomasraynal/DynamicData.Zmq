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
    public class TestDynamicDataE2E_SubscribeToSubject : TestDynamicDataE2E_Base
    {

        [Test]
        public async Task ShouldSubscribeToSubject()
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
                HearbeatEndpoint = HeartbeatEndpoint
            };

            var market1 = GetMarket("FxConnect", marketConfiguration, TimeSpan.FromMilliseconds(500));
            var market2 = GetMarket("Harmony", marketConfiguration, TimeSpan.FromMilliseconds(500));

            var cacheConfigurationEuroDol = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = "EUR/USD"
            };

            var cacheConfigurationEuroDolFxConnect = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = "EUR/USD.FxConnect"
            };

            var cacheEuroDol = GetCache(cacheConfigurationEuroDol);
            var cacheEuroDolFxConnect = GetCache(cacheConfigurationEuroDolFxConnect);

            await router.Run();

            await Task.Delay(1000);

            await market1.Run();
            await market2.Run();

            await Task.Delay(2000);

            await cacheEuroDol.Run();
            await cacheEuroDolFxConnect.Run();

            await WaitForCachesToCaughtUp(cacheEuroDol, cacheEuroDolFxConnect);

            var routerEventCacheItems = (await _eventCache.GetStreamBySubject(string.Empty)).ToList();

            Assert.Greater(routerEventCacheItems.Count(), 0);

            var ccyPairsCacheEuroDol = cacheEuroDol.GetItems()
                                                   .SelectMany(item => item.AppliedEvents)
                                                   .Select(item => item.Subject)
                                                   .Distinct()
                                                   .ToList();

            var ccyPairsCacheEuroDolFxConnect = cacheEuroDolFxConnect.GetItems()
                                                                     .SelectMany(item => item.AppliedEvents)
                                                                     .Select(item => item.Subject)
                                                                     .Distinct()
                                                                     .ToList();

            // EUR/USD.FxConnect & EUR/USD.Harmony
            Assert.AreEqual(2, ccyPairsCacheEuroDol.Count());
            Assert.IsTrue(ccyPairsCacheEuroDol.All(subject => subject.EndsWith("FxConnect") || subject.EndsWith("Harmony")));
            Assert.IsTrue(ccyPairsCacheEuroDol.All(subject => subject.StartsWith(cacheConfigurationEuroDol.Subject)));

            var cacheEvents = cacheEuroDol.GetItems()
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

            // EUR/USD.FxConnect
            Assert.AreEqual(1, ccyPairsCacheEuroDolFxConnect.Count());
            Assert.AreEqual(cacheConfigurationEuroDolFxConnect.Subject, ccyPairsCacheEuroDolFxConnect.First());

        }

    }
}