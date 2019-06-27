using DynamicData;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Dto;
using DynamicData.Event;
using DynamicData.EventCache;
using DynamicData.Shared;
using System.Reactive.Disposables;
using static System.Runtime.CompilerServices.ConfiguredTaskAwaitable;

namespace DynamicData.Cache
{
    public class DynamicCache<TKey, TAggregate> : ActorBase, IDynamicCache<TKey, TAggregate>
        where TAggregate : IAggregate<TKey>, new()
    {
        private readonly IDynamicCacheConfiguration _configuration;
        private readonly CancellationTokenSource _cancel;

        private CompositeDisposable _cleanup;
        private ConfiguredTaskAwaiter _workProc;
        private ConfiguredTaskAwaiter _heartbeatProc;
        private NetMQPoller _cacheUpdatePoller;
        private readonly BehaviorSubject<DynamicCacheState> _state;
        private readonly BehaviorSubject<bool> _isStaled;
      
       // private SubscriberSocket _cacheUpdateSocket;
        private readonly IEventSerializer _eventSerializer;

        private readonly BehaviorSubject<bool> _isCaughtingUp;
        private readonly CaughtingUpCache<TKey, TAggregate> _caughtingUpCache;

        private readonly ManualResetEventSlim _blockEventConsumption;
        private readonly SourceCache<TAggregate, TKey> _sourceCache;

        public DynamicCache(IDynamicCacheConfiguration configuration, IEventSerializer eventSerializer)
        {

            _blockEventConsumption = new ManualResetEventSlim(true);

            _cleanup = new CompositeDisposable();
            _eventSerializer = eventSerializer;
            _configuration = configuration;
            _sourceCache = new SourceCache<TAggregate, TKey>(selector => selector.Id);
            _cancel = new CancellationTokenSource();
            _state = new BehaviorSubject<DynamicCacheState>(DynamicCacheState.NotConnected);
            _isStaled = new BehaviorSubject<bool>(true);
            _isCaughtingUp = new BehaviorSubject<bool>(false);
            _caughtingUpCache = new CaughtingUpCache<TKey, TAggregate>();

        }

        public IObservable<DynamicCacheState> OnStateChanged()
        {
            return _state.AsObservable();
        }

        public IObservableCache<TAggregate, TKey> OnItemChanged()
        {
            return _sourceCache.AsObservableCache();
        }

        public DynamicCacheState CacheState
        {
            get
            {
                return _state.Value;
            }
        }

        public IObservable<bool> OnStaled()
        {
            return _isStaled.AsObservable();
        }
        public bool IsStaled => _isStaled.Value;

        public IObservable<bool> OnCaughtingUp()
        {
            return _isCaughtingUp.AsObservable();
        }
        public bool IsCaughtingUp => _isCaughtingUp.Value;

        public IEnumerable<TAggregate> GetItems() => _sourceCache.Items;

        private IStateReply GetStateOfTheWorld()
        {
            using (var dealer = new DealerSocket())
            {
                var request = new StateRequest()
                {
                    Subject = _configuration.Subject,
                };

                var requestBytes = _eventSerializer.Serializer.Serialize(request);

                dealer.Connect(_configuration.StateOfTheWorldEndpoint);
                dealer.SendFrame(requestBytes);

                var hasResponse = dealer.TryReceiveFrameBytes(_configuration.StateCatchupTimeout, out var responseBytes);

                //todo: retry policy
                if (!hasResponse) throw new Exception("unable to reach broker");

                return _eventSerializer.Serializer.Deserialize<StateReply>(responseBytes);

            }
        }

        //todo: use timer & poller
        private void HandleHeartbeat()
        {
            while (!_cancel.IsCancellationRequested)
            {
                using (var heartbeat = new RequestSocket(_configuration.HearbeatEndpoint))
                {
                    var payload = _eventSerializer.Serializer.Serialize(Heartbeat.Query); 

                    heartbeat.SendFrame(payload);

                    var response = heartbeat.TryReceiveFrameBytes(_configuration.HeartbeatDelay, out var responseBytes);

                    if (_cancel.IsCancellationRequested) return;

                    var currentState = response ? DynamicCacheState.Connected : DynamicCacheState.Disconnected;

                    switch (currentState)
                    {
                        case DynamicCacheState.Connected:

                            if (_state.Value == DynamicCacheState.NotConnected || _state.Value == DynamicCacheState.Reconnected)
                            {
                                _state.OnNext(currentState);
                            }
                            else if (_state.Value == DynamicCacheState.Disconnected)
                            {
                                _state.OnNext(DynamicCacheState.Reconnected);
                            }
                               
                            break;

                        case DynamicCacheState.Disconnected:
                            if (_state.Value == DynamicCacheState.Connected || _state.Value == DynamicCacheState.Reconnected)
                            {
                                _state.OnNext(currentState);
                            }
                               
                            break;
                    }

                }

                Thread.Sleep(_configuration.HeartbeatDelay.Milliseconds);

            }
        }

        private void HandleWork()
        {
            using (var cacheUpdateSocket = new SubscriberSocket())
            {
                cacheUpdateSocket.Options.ReceiveHighWatermark = _configuration.ZmqHighWatermark;
                cacheUpdateSocket.Subscribe(_configuration.Subject);
                cacheUpdateSocket.Connect(_configuration.SubscriptionEndpoint);

                bool isSocketActive = false;

                var isStaleTimer = new NetMQTimer(_configuration.IsStaleTimeout);

                using (_cacheUpdatePoller = new NetMQPoller { cacheUpdateSocket, isStaleTimer })
                {
                    isStaleTimer.Elapsed += (s, e) =>
                    {
                        //if no activity and state is not staled, set as staled
                        if (!isSocketActive && !IsStaled)
                        {
                            _isStaled.OnNext(true);
                        }
   
                        isSocketActive = false;

                    };

                    cacheUpdateSocket.ReceiveReady += (s, e) =>
                    {
                  
                        var message = e.Socket.ReceiveMultipartMessage();

                        if (_cancel.IsCancellationRequested) return;

                        var eventIdBytes = message[1].Buffer;
                        var eventMessageBytes = message[2].Buffer;

                        var eventId = _eventSerializer.Serializer.Deserialize<IEventId>(eventIdBytes);
                        var producerMessage = _eventSerializer.Serializer.Deserialize<IProducerMessage>(eventMessageBytes);

                        var @event = _eventSerializer.ToEvent<TKey, TAggregate>(eventId, producerMessage);

                        OnEventReceived(@event);

                        isSocketActive = true;

                        if (IsStaled)
                        {
                            _isStaled.OnNext(false);
                        }

                    };


                    _cacheUpdatePoller.Run();
                }

            }
        }

        private void ApplyEvent(IEvent<TKey, TAggregate> @event)
        {
            var aggregate = _sourceCache.Lookup(@event.EventStreamId);

            if (!aggregate.HasValue)
            {
                var @new = new TAggregate
                {
                    Id = @event.EventStreamId
                };

                @new.Apply(@event);

                _sourceCache.AddOrUpdate(@new);
            }
            else
            {
                aggregate.Value.Apply(@event);

                _sourceCache.AddOrUpdate(aggregate.Value);
            }
        }

        private void OnEventReceived(IEvent<TKey, TAggregate> @event)
        {

            if (IsCaughtingUp)
            {
                _blockEventConsumption.Wait();

                if (IsCaughtingUp)
                {
                    _caughtingUpCache.CaughtUpEvents.Add(@event);
                    return;
                }

            }

            ApplyEvent(@event);
        }

        private void CaughtUpToStateOfTheWorld()
        {
            //failover policy
            while (CacheState != DynamicCacheState.Connected && CacheState != DynamicCacheState.Reconnected)
            {
                if (_cancel.IsCancellationRequested) return;

                //todo: log attempt
                Thread.Sleep(100);
            }

            _sourceCache.Edit((updater) =>
            {
                updater.Clear();

                //fetch state of the world
                var stateOfTheWorld = GetStateOfTheWorld();

                var update = new Action<IEvent<TKey, TAggregate>>((@event) =>
                {

                    var aggregate = updater.Lookup(@event.EventStreamId);

                    if (!aggregate.HasValue)
                    {
                        var @new = new TAggregate
                        {
                            Id = @event.EventStreamId
                        };

                        @new.Apply(@event);

                        updater.AddOrUpdate(@new);
                    }
                    else
                    {
                        aggregate.Value.Apply(@event);

                        updater.AddOrUpdate(aggregate.Value);
                    }

                });

                //run all events on cache
                foreach (var eventMessage in stateOfTheWorld.Events)
                {
                    var @event = _eventSerializer.ToEvent<TKey, TAggregate>(eventMessage);

                    update(@event);
                }

                //block new events consumption until the caughting up cache is digested
                _blockEventConsumption.Reset();

                var replayEvents = _caughtingUpCache.CaughtUpEvents
                                                .Where(ev => !stateOfTheWorld.Events.Any(msg => msg.EventId.Id == ev.EventId))
                                                .ToList();

                foreach (var @event in replayEvents)
                {
                    update(@event);
                }

            });

            //the cache is up to date
            _isCaughtingUp.OnNext(false);

            //allow event consumption to proceed
            _blockEventConsumption.Set();

            //clear the caughting up cache for further use
            _caughtingUpCache.Clear();
        }

        protected override Task RunInternal()
        {
            //on reconnected start a caughtup process
            var observeCacheState = _state
                .Where(state => state == DynamicCacheState.Reconnected)
                .Subscribe(_ =>
                 {
                     _isCaughtingUp.OnNext(true);
                 });

            _cleanup.Add(observeCacheState);

            //start caughtingup by fetching the broker state of the world
            var caughtingUpState = _isCaughtingUp
                .Where(state => state)
                .Subscribe(_ =>
                {
                    //run on new Task as CaughtUpToStateOfTheWorld is blocking
                    Task.Run(CaughtUpToStateOfTheWorld);
                });

            _cleanup.Add(caughtingUpState);

            _workProc = Task.Run(HandleWork, _cancel.Token)
                            .ConfigureAwait(false)
                            .GetAwaiter();

            _heartbeatProc = Task.Run(HandleHeartbeat, _cancel.Token)
                                 .ConfigureAwait(false)
                                 .GetAwaiter();

            _isCaughtingUp.OnNext(true);

            return Task.CompletedTask;
      
        }

        protected async override Task DestroyInternal()
        {
            _cancel.Cancel();

            _cleanup.Dispose();

            _state.OnCompleted();
            _state.Dispose();

            _sourceCache.Dispose();

            _cacheUpdatePoller.Stop();

            while (!_workProc.IsCompleted || !_heartbeatProc.IsCompleted)
            {
               await Task.Delay(50);
            }
        }
    }
}
