using DynamicData.Demo;
using DynamicData.Producer;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicData.Zmq.Demo
{
    public class HostProducer : IHostedService
    {
        private readonly IProducer<string, CurrencyPair> _producer;

        public HostProducer(IProducer<string, CurrencyPair> producer)
        {
            _producer = producer;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _producer.Run();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _producer.Destroy();
        }
    }
}
