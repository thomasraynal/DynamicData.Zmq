using DynamicData.Broker;
using DynamicData.EventCache;
using DynamicData.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicData.Zmq.Demo
{
    public class HostBroker : IHostedService
    {
        private readonly IBrokerageService _broker;

        public HostBroker(IBrokerageService broker)
        {
            _broker = broker;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _broker.Run();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _broker.Destroy();
        }
    }
}
