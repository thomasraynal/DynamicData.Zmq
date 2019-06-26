﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroMQPlayground.DynamicData.Broker;
using ZeroMQPlayground.DynamicData.Cache;
using ZeroMQPlayground.DynamicData.Producer;

namespace ZeroMQPlayground.DynamicData.E2E
{
    [TestFixture]
    public class TestDynamicDataE2E_ConnectAndDisconnectFromBroker: TestDynamicDataE2E_Base
    {
 
        [Test]
        public async Task ShouldConnectAndDisconnectFromBroker()
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
                HearbeatEndpoint = HeartbeatEndpoint,
                HeartbeatDelay = TimeSpan.FromMilliseconds(500),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromMilliseconds(500),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var router = GetBrokerageService(brokerConfiguration);
            var market = GetMarket("FxConnect", marketConfiguration, TimeSpan.FromMilliseconds(1000));
            var cache =  GetCache(cacheConfiguration);

            var cacheStates = new List<DynamicCacheState>();

            var stateObservable = cache.OnStateChanged()
                                       .Subscribe(state =>
                                       {
                                           cacheStates.Add(state);
                                       });

            
            await market.Run();
            await cache.Run();

            Assert.AreEqual(ProducerState.NotConnected, market.ProducerState);
            Assert.AreEqual(DynamicCacheState.NotConnected, cache.CacheState);

            await router.Run();

            await Task.Delay(1000);

            await WaitForCachesToCaughtUp(cache);

            Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);
            Assert.AreEqual(ProducerState.Connected, market.ProducerState);

            await router.Destroy();

            await Task.Delay(2000);

            Assert.AreEqual(DynamicCacheState.Disconnected, cache.CacheState);
            Assert.AreEqual(ProducerState.Disconnected, market.ProducerState);

            router = GetBrokerageService(brokerConfiguration);

            await router.Run();

            await Task.Delay(3000);

            Assert.AreEqual(5, cacheStates.Count);
            Assert.AreEqual(DynamicCacheState.NotConnected, cacheStates.ElementAt(0));
            Assert.AreEqual(DynamicCacheState.Connected, cacheStates.ElementAt(1));
            Assert.AreEqual(DynamicCacheState.Disconnected, cacheStates.ElementAt(2));
            Assert.AreEqual(DynamicCacheState.Reconnected, cacheStates.ElementAt(3));
            Assert.AreEqual(DynamicCacheState.Connected, cacheStates.ElementAt(4));

            Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);
            Assert.AreEqual(ProducerState.Connected, market.ProducerState);

            stateObservable.Dispose();


        }
    }
}