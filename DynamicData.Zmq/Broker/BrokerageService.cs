using NetMQ;
using NetMQ.Sockets;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQPlayground.DynamicData.Broker;
using ZeroMQPlayground.DynamicData.Dto;
using ZeroMQPlayground.DynamicData.EventCache;
using ZeroMQPlayground.DynamicData.Serialization;
using ZeroMQPlayground.DynamicData.Shared;

namespace ZeroMQPlayground.DynamicData.Broker
{
    public class BrokerageService : ActorBase
    {

        private readonly CancellationTokenSource _cancel;

        private ConfiguredTaskAwaitable _workProc;
        private ConfiguredTaskAwaitable _heartbeartProc;
        private ConfiguredTaskAwaitable _stateOfTheWorldProc;

        private NetMQPoller _poller;
        private ResponseSocket _heartbeatSocket;
        private RouterSocket _stateRequestSocket;

        private readonly IEventCache _cache;
        private readonly ISerializer _serializer;
        private readonly IBrokerageServiceConfiguration _configuration;

        public BrokerageService(IBrokerageServiceConfiguration configuration, IEventCache cache,ISerializer serializer)
        {
            _cache = cache;
            _serializer = serializer;
            _configuration = configuration;

            _cancel = new CancellationTokenSource();
        }

        protected override Task RunInternal()
        {
            _workProc = Task.Run(HandleWork, _cancel.Token).ConfigureAwait(false);
            _heartbeartProc = Task.Run(HandleHeartbeat, _cancel.Token).ConfigureAwait(false);
            _stateOfTheWorldProc = Task.Run(HandleStateOfTheWorldRequest, _cancel.Token).ConfigureAwait(false);

            return Task.CompletedTask;
        }

        protected override Task DestroyInternal()
        {
            _cancel.Cancel();

            _poller.Stop();
            
            _heartbeatSocket.Close();
            _heartbeatSocket.Dispose();

            _stateRequestSocket.Close();
            _stateRequestSocket.Dispose();

            return Task.CompletedTask;

        }

        public void HandleHeartbeat()
        {
            using (_heartbeatSocket = new ResponseSocket(_configuration.HeartbeatEndpoint))
            {
                while (!_cancel.IsCancellationRequested)
                {
                    var messageBytes = _heartbeatSocket.ReceiveFrameBytes();

                    if (_cancel.IsCancellationRequested) return;

                    _heartbeatSocket.SendFrame(_serializer.Serialize(Heartbeat.Response));

                }
            }
        }

        private async Task HandleStateOfTheWorldRequest()
        {
            using (_stateRequestSocket = new RouterSocket())
            {
                _stateRequestSocket.Bind(_configuration.StateOftheWorldEndpoint);

                while (!_cancel.IsCancellationRequested)
                {
                    var message = _stateRequestSocket.ReceiveMultipartMessage();
                    var sender = message[0].Buffer;
                    var request = _serializer.Deserialize<IStateRequest>(message[1].Buffer);

                    var stream = await _cache.GetStreamBySubject(request.Subject);

                    var response = new StateReply()
                    {
                        Subject = request.Subject,
                        Events = stream.ToList()
                    };

                    _stateRequestSocket.SendMoreFrame(sender)
                                       .SendFrame(_serializer.Serialize(response));

                }
            }
        }

        private void HandleWork()
        {
            using (var stateUpdate = new SubscriberSocket())
            {
                stateUpdate.SubscribeToAnyTopic();
                stateUpdate.Bind(_configuration.ToPublisherEndpoint);

                stateUpdate.Options.ReceiveHighWatermark = _configuration.ZmqHighWatermark;

                using (var stateUpdatePublish = new PublisherSocket())
                {
                    stateUpdatePublish.Bind(_configuration.ToSubscribersEndpoint);

                    stateUpdate.ReceiveReady += async (s, e) =>
                            {
                           
                                var message = e.Socket.ReceiveMultipartMessage();

                                var subject = message[0].ConvertToString();
                                var payload = message[1];

                                var eventId = await _cache.AppendToStream(subject, payload.Buffer);

                                stateUpdatePublish.SendMoreFrame(message[0].Buffer)
                                                  .SendMoreFrame(_serializer.Serialize(eventId))
                                                  .SendFrame(payload.Buffer);
                            };

                    using (_poller = new NetMQPoller { stateUpdate, stateUpdatePublish })
                    {
                        _poller.Run();
                    }
                }
            }
        }
    }
}
