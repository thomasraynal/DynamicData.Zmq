﻿using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using DynamicData.Zmq.Broker;
using DynamicData.Zmq.Cache;
using DynamicData.Zmq.Producer;
using DynamicData.Zmq.Demo;
using System.Collections.Generic;

namespace DynamicData.Tests.E2E
{
    [TestFixture]
    public class TestDynamicDataE2E_HandleDisconnectAndRebuildCache : TestDynamicDataE2E_Base
    {

        [Test]
        public async Task ShouldHandleDisconnectAndCacheRebuild()
        {

            var brokerConfiguration = new BrokerageServiceConfiguration()
            {
                HeartbeatEndpoint = HeartbeatEndpoint,
                StateOfTheWorldEndpoint = StateOfTheWorldEndpoint,
                ToSubscribersEndpoint = ToSubscribersEndpoint,
                ToPublisherEndpoint = ToPublishersEndpoint
            };

            var router = GetBrokerageService(brokerConfiguration);
          
            var marketConfiguration = new MarketConfiguration("FxConnect")
            {
                BrokerEndpoint = ToPublishersEndpoint,
                HeartbeatEndpoint = HeartbeatEndpoint,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1),
                IsAutoGen = false
            };

            var market1 = GetMarket(marketConfiguration);

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(1)
            };

            var cache = GetCache(cacheConfiguration);
            var cacheProof = GetCache(cacheConfiguration);

            var cacheStates = new List<DynamicCacheState>();

            var stateObservable = cache.OnStateChanged
                           .Subscribe(state =>
                           {
                               cacheStates.Add(state);
                           });


            await router.Run();
            await market1.Run();
            await cache.Run();
            await cacheProof.Run();

            Assert.AreEqual(DynamicCacheState.NotConnected, cache.CacheState);
            Assert.AreEqual(DynamicCacheState.NotConnected, cacheProof.CacheState);

            await Task.Delay(1000);

            await market1.WaitUntilConnected();

            Assert.AreEqual(ProducerState.Connected, market1.ProducerState);

            market1.PublishNext();
            market1.PublishNext();
            market1.PublishNext();

            await WaitForCachesToCaughtUp(cache, cacheProof);

            Assert.AreEqual(DynamicCacheState.Connected, cache.CacheState);
            Assert.AreEqual(DynamicCacheState.Connected, cacheProof.CacheState);

            var cacheEvents = cache.Items.SelectMany(item => item.AppliedEvents).ToList();
            var cacheProofEvents = cacheProof.Items.SelectMany(item => item.AppliedEvents).ToList();

            Assert.AreEqual(cacheEvents.Count(), cacheProofEvents.Count());

            await router.Destroy();

            await Task.Delay(2000);

            Assert.AreEqual(DynamicCacheState.Disconnected, cache.CacheState);
            Assert.AreEqual(DynamicCacheState.Disconnected, cacheProof.CacheState);

            router = GetBrokerageService(brokerConfiguration);

            await router.Run();

            await Task.Delay(1000);

            market1.PublishNext();
            market1.PublishNext();
            market1.PublishNext();

            await WaitForCachesToCaughtUp(cache, cacheProof);

            var cacheCCyPair = cache.Items.ToList();
            var cacheProofCcyPair = cacheProof.Items.ToList();

            Assert.AreEqual(cacheCCyPair.Count(), cacheProofCcyPair.Count());
            Assert.AreEqual(cacheCCyPair.Count(), cacheProofCcyPair.Count());

            foreach (var ccyPair in cacheCCyPair)
            {
                var proof = cacheProofCcyPair.First(ccy => ccy.Id == ccyPair.Id);

                Assert.AreEqual(ccyPair.Ask, proof.Ask);
                Assert.AreEqual(ccyPair.Bid, proof.Bid);
                Assert.AreEqual(ccyPair.Mid, proof.Mid);
                Assert.AreEqual(ccyPair.Spread, proof.Spread);
            }

            cacheEvents = cacheCCyPair.SelectMany(item => item.AppliedEvents).ToList();
            cacheProofEvents = cacheProofCcyPair.SelectMany(item => item.AppliedEvents).ToList();

            Assert.AreEqual(cacheEvents.Count(), cacheProofEvents.Count());
            Assert.AreEqual(cacheEvents.Count(), cacheProofEvents.Count());
            Assert.AreEqual(cacheEvents.Count(), cacheProofEvents.Count());

            await Task.Delay(1000);

            Assert.AreEqual(5, cacheStates.Count);
            Assert.AreEqual(DynamicCacheState.NotConnected, cacheStates.ElementAt(0));
            Assert.AreEqual(DynamicCacheState.Connected, cacheStates.ElementAt(1));
            Assert.AreEqual(DynamicCacheState.Disconnected, cacheStates.ElementAt(2));
            Assert.AreEqual(DynamicCacheState.Reconnected, cacheStates.ElementAt(3));
            Assert.AreEqual(DynamicCacheState.Connected, cacheStates.ElementAt(4));

            stateObservable.Dispose();

        }

    }
}
