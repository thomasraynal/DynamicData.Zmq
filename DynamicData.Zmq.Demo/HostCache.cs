using DynamicData.Cache;
using DynamicData.Demo;
using DynamicData.Event;
using DynamicData.Zmq.Demo.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicData.Zmq.Demo
{
    public class HostCache : IHostedService
    {
        private readonly IDynamicCache<string, CurrencyPair> _cache;
        private readonly ILogger<HostCache> _logger;

        public HostCache(ILogger<HostCache> logger, IDynamicCache<string, CurrencyPair> cache)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {

            _cache.OnItemChanged
                  .Connect()
                  .ToCollection()
                  .Scan(CcyPairPrices.Default, (previous, obs) =>
                    {
                        var prices = new CcyPairPrices();

                        foreach (var o in obs)
                        {
                            prices.Prices.Add(new Price()
                            {
                                CcyPair = o.Id,
                                Ask = o.Ask,
                                Bid = o.Bid,
                                EventCount = o.AppliedEvents.Count()
                            });
                        }

                        return prices;

                    })
                  .Subscribe(state =>
                {
                    _logger.LogInformation(state.ToString());
                }
        );


            await _cache.Run();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _cache.Destroy();
        }
    }
}
