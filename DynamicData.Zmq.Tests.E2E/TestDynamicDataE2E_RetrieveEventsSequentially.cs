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
    public class TestDynamicDataE2E_RetrieveEventsSequentially : TestDynamicDataE2E_Base
    {

        [Test]
        public async Task ShouldRetrievedEventsSequentially()
        {
    
            var brokerConfiguration = new BrokerageServiceConfiguration()
            {
                HeartbeatEndpoint = HeartbeatEndpoint,
                StateOfTheWorldEndpoint = StateOfTheWorldEndpoint,
                ToSubscribersEndpoint = ToSubscribersEndpoint,
                ToPublisherEndpoint = ToPublishersEndpoint
            };

            var marketConfigurationFxConnect = new MarketConfiguration("FxConnect")
            {
                BrokerEndpoint = ToPublishersEndpoint,
                HeartbeatEndpoint = HeartbeatEndpoint
            };

            var marketConfigurationHarmony = new MarketConfiguration("Harmony")
            {
                BrokerEndpoint = ToPublishersEndpoint,
                HeartbeatEndpoint = HeartbeatEndpoint
            };

            var cacheConfiguration = new DynamicCacheConfiguration(ToSubscribersEndpoint, StateOfTheWorldEndpoint, HeartbeatEndpoint)
            {
                Subject = string.Empty,
                HeartbeatDelay = TimeSpan.FromSeconds(10),
                HeartbeatTimeout = TimeSpan.FromSeconds(2)
            };

            var router = GetBrokerageService(brokerConfiguration);

            var market1 = GetMarket(marketConfigurationFxConnect);
            var market2 = GetMarket(marketConfigurationHarmony);

            var cache = GetCache(cacheConfiguration);
            var cacheProof = GetCache(cacheConfiguration);

            await router.Run();

            await market1.Run();
            await market2.Run();

            await cache.Run();
            await cacheProof.Run();

            await WaitForCachesToCaughtUp(cache, cacheProof);

            var cacheEvents = cache.Items
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


            var cacheProofEvents = cache.Items
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
