using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Zmq.Event;
using DynamicData.Zmq.Producer;
using Microsoft.Extensions.Logging;

namespace DynamicData.Zmq.Demo
{
    public class Market : ProducerBase<string, CurrencyPair>
    {

        private static readonly string[] CcyPairs = { "EUR/USD", "EUR/GBP" };

        private readonly CancellationTokenSource _cancel;
        private Task _workProc;
        private readonly Random _rand = new Random();

        private readonly MarketConfiguration _configuration;

        public List<ChangeCcyPairPrice> Prices { get; set; }

        public Market(MarketConfiguration configuration, ILogger<IProducer<string, CurrencyPair>> logger, IEventSerializer eventSerializer) : base(configuration, logger, eventSerializer)
        {

            _cancel = new CancellationTokenSource();

            _configuration = configuration;

            Prices = new List<ChangeCcyPairPrice>();

            OnDestroyed += async () =>
            {
                _cancel.Cancel();
                await WaitForWorkProceduresToComplete(_workProc);
            };

            OnRunning += () =>
            {
                if (_configuration.IsAutoGen)
                {
                    _workProc = Task.Run(HandleWork, _cancel.Token);
                }
                else
                {
                    _workProc = Task.CompletedTask;
                }

            };

        }
        public ChangeCcyPairPrice Next()
        {
            var mid = _rand.NextDouble() * 10;
            var spread = _rand.NextDouble() * 2;

            var topic = CcyPairs[_rand.Next(0, CcyPairs.Count())];

            var price = new ChangeCcyPairPrice(
                ask: mid + spread,
                bid: mid - spread,
                mid: mid,
                spread: spread,
                ccyPairId: topic,
                market: _configuration.Name
            );

            return price;
        }

        public void PublishNext()
        {
            Publish(Next());
        }

        public async Task WaitUntilConnected()
        {
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(500);
                if (ProducerState == ProducerState.Connected) return;
            }

        }

        private void HandleWork()
        {
            while (!_cancel.IsCancellationRequested)
            {

                Thread.Sleep(_configuration.PriceGenerationDelay);

                if (_cancel.IsCancellationRequested) return;

                if (ProducerState == ProducerState.Connected)
                {
                    var changePrice = Next();

                    Publish(changePrice);
                    Prices.Add(changePrice);
                }
            }
        }
    }
}
