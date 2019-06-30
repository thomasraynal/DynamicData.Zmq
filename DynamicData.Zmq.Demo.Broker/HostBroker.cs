using DynamicData.Zmq.Broker;
using Microsoft.Extensions.Hosting;
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
