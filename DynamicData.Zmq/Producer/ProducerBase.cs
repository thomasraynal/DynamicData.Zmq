using NetMQ;
using NetMQ.Sockets;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Dto;
using DynamicData.Event;
using DynamicData.Shared;
using static System.Runtime.CompilerServices.ConfiguredTaskAwaitable;

namespace DynamicData.Producer
{
    public abstract class ProducerBase<TKey,TAggregate> : ActorBase where TAggregate : IAggregate<TKey>
    {

        private readonly IEventSerializer _eventSerializer;
        private readonly IProducerConfiguration _configuration;
        private readonly CancellationTokenSource _cancel;
        private readonly BehaviorSubject<ProducerState> _state;
        private Task _heartbeatProc;
        private PublisherSocket _publisherSocket;
        private NetMQPoller _heartbeatPoller;

        public ProducerState ProducerState
        {
            get
            {
                return _state.Value;
            }
        }

        public IObservable<ProducerState> OnStateChanged()
        {
            return _state.AsObservable();
        }

        public ProducerBase(IProducerConfiguration producerConfiguration, IEventSerializer eventSerializer)
        {
            _eventSerializer = eventSerializer;
            _configuration = producerConfiguration;
            _cancel = new CancellationTokenSource();

            _state = new BehaviorSubject<ProducerState>(ProducerState.NotConnected);
        }

        protected override Task RunInternal()
        {
            _publisherSocket = new PublisherSocket();
            _publisherSocket.Connect(_configuration.RouterEndpoint);
            _heartbeatProc = Task.Run(HandleHeartbeat, _cancel.Token);

            return Task.CompletedTask;
        }

        protected async override Task DestroyInternal()
        {
            _cancel.Cancel();

            _state.OnCompleted();
            _state.Dispose();

            _publisherSocket.Close();
            _publisherSocket.Dispose();

            _heartbeatPoller.Stop();

            await WaitForWorkProceduresToComplete(_heartbeatProc);

        }

        //todo : actor IHeartbeatChecker IHeartbeatHandler 
        private void HandleHeartbeat()
        {
            var heartbeatTimer = new NetMQTimer(_configuration.HeartbeatDelay);

            using (_heartbeatPoller = new NetMQPoller { heartbeatTimer })
            {

                var runHeartBeat = new Action(() =>
                {

                    if (_cancel.IsCancellationRequested) return;

                    //todo: handle zombie socket
                    using (var heartbeat = new RequestSocket(_configuration.HeartbeatEndpoint))
                    {

                        var query = _eventSerializer.Serializer.Serialize(Heartbeat.Query);

                        heartbeat.SendFrame(query);

                        var response = heartbeat.TryReceiveFrameBytes(_configuration.HeartbeatTimeout, out var responseBytes);

                        if (_cancel.IsCancellationRequested) return;

                        var currentState = response ? ProducerState.Connected : ProducerState.Disconnected;

                        switch (currentState)
                        {
                            case ProducerState.Connected:

                                if (_state.Value == ProducerState.NotConnected || _state.Value == ProducerState.Disconnected)
                                {
                                    _state.OnNext(currentState);
                                }

                                break;

                            case ProducerState.Disconnected:

                                if (_state.Value == ProducerState.Connected)
                                {
                                    _state.OnNext(currentState);
                                }

                                break;
                        }
                    }
                });

                heartbeatTimer.Elapsed += (s, e) =>
                {
                    runHeartBeat();
                };

                //hearbeat at once
                runHeartBeat();

                _heartbeatPoller.Run();

            }
        }

        public void Publish(IEvent<TKey, TAggregate> @event)
        {
            if (_state.Value != ProducerState.Connected) throw new InvalidOperationException("publisher is not connected");

            var message = _eventSerializer.ToProducerMessage(@event);

            _publisherSocket.SendMoreFrame(message.Subject)
                            .SendFrame(_eventSerializer.Serializer.Serialize(message));
        }
    }
}
