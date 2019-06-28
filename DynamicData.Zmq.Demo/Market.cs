using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Event;
using DynamicData.Producer;

namespace DynamicData.Demo
{
    public class Market : ProducerBase<string,CurrencyPair>
    {

        private static readonly string[] CcyPairs = { "EUR/USD", "EUR/GBP" };

        private readonly string _name;
        private readonly CancellationTokenSource _cancel;
        private Task _workProc;
        private readonly Random _rand = new Random();
        private readonly TimeSpan _priceGenerationDelay;

        public List<ChangeCcyPairPrice> Prices { get; set; }

        public Market(String name, IProducerConfiguration configuration, IEventSerializer eventSerializer, TimeSpan priceGenerationDelay, bool autoGen= true) : base(configuration,eventSerializer)
        {
         
            _name = name;
            _cancel = new CancellationTokenSource();

            _priceGenerationDelay = priceGenerationDelay;

            Prices = new List<ChangeCcyPairPrice>();

            OnDestroyed += async () =>
            {
                _cancel.Cancel();
                await this.WaitForWorkProceduresToComplete(_workProc);
            };

            OnRunning += () =>
            {
                if (autoGen)
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
                market: _name
            );

            return price;
        }

        public void PublishNext()
        {
            Publish(Next());
        }


        private void HandleWork()
        {
            while (!_cancel.IsCancellationRequested)
            {
  
                Thread.Sleep(_priceGenerationDelay);

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
