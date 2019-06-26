using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQPlayground.DynamicData.Event;
using ZeroMQPlayground.DynamicData.Producer;

namespace ZeroMQPlayground.DynamicData.Demo
{
    public class Market : ProducerBase<string,CurrencyPair>
    {

        private static readonly string[] CcyPairs = { "EUR/USD", "EUR/GBP" };

        private readonly string _name;
        private readonly CancellationTokenSource _cancel;
        private ConfiguredTaskAwaitable _workProc;
        private readonly Random _rand = new Random();
        private readonly TimeSpan _priceGenerationDelay;

        public List<ChangeCcyPairPrice> Prices { get; set; }

        public Market(String name, IProducerConfiguration configuration, IEventSerializer eventSerializer, TimeSpan priceGenerationDelay) : base(configuration,eventSerializer)
        {
            _priceGenerationDelay = priceGenerationDelay;
            _name = name;
            _cancel = new CancellationTokenSource();

            Prices = new List<ChangeCcyPairPrice>();

            OnDestroyed += () =>
            {
                _cancel.Cancel();
            };

            OnRunning += () =>
            {
                _workProc = Task.Run(HandleWork, _cancel.Token).ConfigureAwait(false);
            };

        }
        private ChangeCcyPairPrice Next()
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
