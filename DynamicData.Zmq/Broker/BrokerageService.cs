using NetMQ;
using NetMQ.Sockets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Dto;
using DynamicData.EventCache;
using DynamicData.Serialization;
using DynamicData.Shared;
using Microsoft.Extensions.Logging;

namespace DynamicData.Broker
{
    //todo: error handling
    public class BrokerageService : ActorBase, IBrokerageService
    {

        private readonly CancellationTokenSource _cancel;

        private Task _workProc;
        private Task _heartbeatProc;
        private Task _stateOfTheWorldProc;

        private NetMQPoller _workPoller;
        private NetMQPoller _heartbeatPoller;
        private NetMQPoller _stateRequestPoller;

        private readonly IEventCache _cache;
        private readonly ISerializer _serializer;
        private readonly IBrokerageServiceConfiguration _configuration;

        public BrokerageService(IBrokerageServiceConfiguration configuration, ILogger<BrokerageService> logger, IEventCache cache, ISerializer serializer) : base(logger)
        {
            _cache = cache;
            _serializer = serializer;
            _configuration = configuration;

            _cancel = new CancellationTokenSource();
        }

        protected override Task RunInternal()
        {
            _workProc = Task.Run(HandleWork, _cancel.Token);

            _heartbeatProc = Task.Run(HandleHeartbeat, _cancel.Token);

            _stateOfTheWorldProc = Task.Run(HandleStateOfTheWorldRequest, _cancel.Token);

            return Task.CompletedTask;
        }

        protected override async Task DestroyInternal()
        {
            _cancel.Cancel();

            _workPoller.Stop();
            _heartbeatPoller.Stop();
            _stateRequestPoller.Stop();

            await WaitForWorkProceduresToComplete(_stateOfTheWorldProc, _workProc, _heartbeatProc);

        }

        private void HandleHeartbeat()
        {
            using (var heartbeatSocket = new ResponseSocket(_configuration.HeartbeatEndpoint))
            {
                using (_heartbeatPoller = new NetMQPoller { heartbeatSocket })
                {
                    heartbeatSocket.ReceiveReady += (s, e) =>
                    {
                        var messageBytes = e.Socket.ReceiveFrameBytes();

                        if (_cancel.IsCancellationRequested) return;

                        e.Socket.SendFrame(_serializer.Serialize(Heartbeat.Response));
                    };

                    _heartbeatPoller.Run();
                }
            }
        }

        private Task HandleStateOfTheWorldRequest()
        {
            using (var stateRequestSocket = new RouterSocket())
            {
                stateRequestSocket.Bind(_configuration.StateOfTheWorldEndpoint);

                using (_stateRequestPoller = new NetMQPoller { stateRequestSocket })
                {
                    stateRequestSocket.ReceiveReady += async (s, e) =>
                     {

                         var message = e.Socket.ReceiveMultipartMessage();
                         var sender = message[0].Buffer;
                         var request = _serializer.Deserialize<IStateRequest>(message[1].Buffer);

                         var stream = await _cache.GetStreamBySubject(request.Subject);

                         var response = new StateReply()
                         {
                             Subject = request.Subject,
                             Events = stream.ToList()
                         };

                         e.Socket.SendMoreFrame(sender)
                                            .SendFrame(_serializer.Serialize(response));

                     };

                    _stateRequestPoller.Run();
                }
            }

            return Task.CompletedTask;
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

                    using (_workPoller = new NetMQPoller { stateUpdate, stateUpdatePublish })
                    {
                        _workPoller.Run();
                    }
                }
            }
        }
    }
}
