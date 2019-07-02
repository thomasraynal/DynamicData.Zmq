using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Zmq.Dto;
using DynamicData.Zmq.Event;
using DynamicData.Zmq.EventCache;
using DynamicData.Zmq.Shared;
using System.Reactive.Disposables;
using Polly;
using Polly.Retry;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using System.Collections.Specialized;
using DynamicData.Zmq.Aggregate;

namespace DynamicData.Zmq.Cache
{
    public class DynamicCache<TKey, TAggregate> : ActorBase, IDynamicCache<TKey, TAggregate>
        where TAggregate : IAggregate<TKey>, new()
    {
        private readonly IDynamicCacheConfiguration _configuration;
        private readonly CancellationTokenSource _cancel;

        private readonly CompositeDisposable _cleanup;

        private Task _workProc;
        private Task _heartbeatProc;
        private Task _caughtUpWithStateOfTheWorldProc;

        private NetMQPoller _cacheUpdatePoller;
        private NetMQPoller _heartbeatPoller;

        private readonly BehaviorSubject<DynamicCacheState> _state;
        private readonly BehaviorSubject<bool> _isStaled;

        private readonly IEventSerializer _eventSerializer;

        private readonly BehaviorSubject<bool> _isCaughtingUp;
        private readonly CaughtingUpCache<TKey, TAggregate> _caughtingUpCache;

        private readonly ManualResetEventSlim _blockEventConsumption;
        private readonly ObservableCollection<ActorMonitoringError> _cacheErrors;
        private readonly ILogger _logger;
        private readonly SourceCache<TAggregate, TKey> _sourceCache;

        private readonly RetryPolicy _getStateOfTheWorldRetyPolicy;

        public DynamicCache(IDynamicCacheConfiguration configuration, ILogger<DynamicCache<TKey, TAggregate>> logger, IEventSerializer eventSerializer) : base(logger)
        {
          
            _blockEventConsumption = new ManualResetEventSlim(true);

            _cacheErrors = new ObservableCollection<ActorMonitoringError>();

            _logger = logger;
            _cleanup = new CompositeDisposable();
            _eventSerializer = eventSerializer;
            _configuration = configuration;
            _sourceCache = new SourceCache<TAggregate, TKey>(selector => selector.Id);
            _cancel = new CancellationTokenSource();

            _state = new BehaviorSubject<DynamicCacheState>(DynamicCacheState.NotConnected);
            _isStaled = new BehaviorSubject<bool>(true);
            _isCaughtingUp = new BehaviorSubject<bool>(false);

            _caughtingUpCache = new CaughtingUpCache<TKey, TAggregate>();

            _getStateOfTheWorldRetyPolicy = Policy.Handle<Exception>()
                                                  .RetryForever((ex) =>
                                                  {
                                                      _cacheErrors.Add(new ActorMonitoringError()
                                                      {
                                                          CacheErrorStatus = ActorErrorType.DynamicCacheGetStateOfTheWorldFailure,
                                                          Exception = ex
                                                      });

                                                  });


            //on reconnected start try to caught up
            _state.Subscribe(state =>
            {
                _logger.LogInformation($"Cache state is {state}");

                if (state == DynamicCacheState.Reconnected)
                {
                    _isCaughtingUp.OnNext(true);
                }

            }).Cleanup(_cleanup);


            //start caughting up by fetching the broker state of the world
            _isCaughtingUp
                .Where(state => state)
                .Subscribe(_ => 
                
                    _caughtUpWithStateOfTheWorldProc = Task.Run(() =>
                               {
                                   WaitUntilConnected();

                                   WaitUntilCaughtUpToStateOfTheWorld();

                               }, _cancel.Token))

                 .Cleanup(_cleanup);


            Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>
                            (e => Errors.CollectionChanged += e, e => Errors.CollectionChanged -= e)
                      .Where(arg => arg.EventArgs.NewItems.Count > 0)
                      .Subscribe(arg =>
                      {
                          foreach (var error in arg.EventArgs.NewItems.Cast<ActorMonitoringError>())
                          {
                              _logger.LogError(error.Message, error.Exception);
                          }
                      })
                      .Cleanup(_cleanup);

        }
        public IObservable<DynamicCacheState> OnStateChanged => _state.AsObservable();
        public DynamicCacheState CacheState => _state.Value;
        public IObservable<bool> OnStaled => _isStaled.AsObservable();
        public IObservableCache<TAggregate, TKey> OnItemChanged => _sourceCache.AsObservableCache();
        public bool IsStaled => _isStaled.Value;
        public IObservable<bool> OnCaughtingUp => _isCaughtingUp.AsObservable();
        public bool IsCaughtingUp => _isCaughtingUp.Value;
        public IEnumerable<TAggregate> Items => _sourceCache.Items;
        public ObservableCollection<ActorMonitoringError> Errors => _cacheErrors;

        private Task<IStateReply> GetStateOfTheWorld()
        {
            var policyResult = _getStateOfTheWorldRetyPolicy.ExecuteAndCapture<IStateReply>(() =>
            {
                using (var dealer = new DealerSocket())
                {
                    dealer.Connect(_configuration.StateOfTheWorldEndpoint);

                    var request = new StateRequest(_configuration.Subject);

                    var requestBytes = _eventSerializer.Serializer.Serialize(request);

                    dealer.SendFrame(requestBytes);

                    var hasResponse = dealer.TryReceiveFrameBytes(_configuration.StateCatchupTimeout, out var responseBytes);

                    if (!hasResponse) throw new UnreachableBrokerException($"Unable to reach broker @{_configuration.StateOfTheWorldEndpoint}");

                    return _eventSerializer.Serializer.Deserialize<StateReply>(responseBytes);

                }

            });

            return Task.FromResult(policyResult.Result);

        }

        private void HandleHeartbeat()
        {

            var heartbeatTimer = new NetMQTimer(_configuration.HeartbeatDelay);

            using (_heartbeatPoller = new NetMQPoller { heartbeatTimer })
            {
                var runHeartBeat = new Action(() =>
                {
                    if (_cancel.IsCancellationRequested) return;

                    using (var heartbeat = new RequestSocket(_configuration.HeartbeatEndpoint))
                    {

                        var payload = _eventSerializer.Serializer.Serialize(Heartbeat.Query);

                        heartbeat.SendFrame(payload);

                        var hasResponse = heartbeat.TryReceiveFrameBytes(_configuration.HeartbeatDelay, out var responseBytes);

                        if (_cancel.IsCancellationRequested) return;

                        var currentState = hasResponse ? DynamicCacheState.Connected : DynamicCacheState.Disconnected;

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
                        try
                        {

                            NetMQMessage message = null;

                            while (e.Socket.TryReceiveMultipartMessage(ref message))
                            {

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
                            }

                        }
                        catch (Exception ex)
                        {
                            _cacheErrors.Add(new ActorMonitoringError()
                            {
                                CacheErrorStatus = ActorErrorType.DynamicCacheEventHandlingFailure,
                                Exception = ex
                            });
                        }

                    };


                    _cacheUpdatePoller.Run();
                }

            }
        }

        private void ApplyEvent(IEvent<TKey, TAggregate> @event, Action<TAggregate> apply)
        {
            var aggregate = _sourceCache.Lookup(@event.EventStreamId);

            if (!aggregate.HasValue)
            {
                var @new = new TAggregate
                {
                    Id = @event.EventStreamId
                };

                @new.Apply(@event);

                apply(@new);
            }
            else
            {
                aggregate.Value.Apply(@event);

                apply(aggregate.Value);
            }
        }

        private void OnEventReceived(IEvent<TKey, TAggregate> @event)
        {

            //if the gate is down:
            //1) either we're caughting up, and thus using the _caughtingUpCache to keep the ongoing event stream
            //2) either we're not and we update the _sourceCache
            //if the gate is up, we're reconcialiting the _sourceCache with the _caughtingUpCache - hence, when the gate is down again IsCaughtingUp has been set to false and we update the _sourceCache
            _blockEventConsumption.Wait();

            if (IsCaughtingUp)
            {
                _caughtingUpCache.CaughtUpEvents.Add(@event);
                return;
            }

            ApplyEvent(@event, (aggregate) => _sourceCache.AddOrUpdate(aggregate));
        }

        private void WaitUntilConnected()
        {
            _logger.LogInformation($"Try to caught up with state of the world");

            while (CacheState != DynamicCacheState.Connected && CacheState != DynamicCacheState.Reconnected)
            {
                if (_cancel.IsCancellationRequested) return;

                Thread.Sleep(100);
            }
        }

        private void WaitUntilCaughtUpToStateOfTheWorld()
        {
            _sourceCache.Edit(async (updater) =>
            {
                updater.Clear();

                _logger.LogInformation($"Try to get state of the world");

                var stateOfTheWorld = await GetStateOfTheWorld();

                //run all events on cache
                foreach (var eventMessage in stateOfTheWorld.Events)
                {
                    var @event = _eventSerializer.ToEvent<TKey, TAggregate>(eventMessage);

                    ApplyEvent(@event, (aggregate) => updater.AddOrUpdate(aggregate));
                }

                //block new events consumption until the caughting up cache is digested
                _blockEventConsumption.Reset();

                //keep only relevant events
                var replayEvents = _caughtingUpCache.CaughtUpEvents
                                                .Where(ev => !stateOfTheWorld.Events.Any(msg => msg.EventId.Id == ev.EventId))
                                                .ToList();

                foreach (var @event in replayEvents)
                {
                    ApplyEvent(@event, (aggregate) => updater.AddOrUpdate(aggregate));
                }

            });

            _logger.LogInformation($"Caught up with state of the world");

            //the cache is up to date, first notify the caughting up process end...
            _isCaughtingUp.OnNext(false);

            //...then allow event consumption to proceed
            _blockEventConsumption.Set();

            //clear the caughting up cache for further use
            _caughtingUpCache.Clear();
        }

        protected override Task RunInternal()
        {

            _caughtUpWithStateOfTheWorldProc = Task.CompletedTask;

            _workProc = Task.Run(HandleWork, _cancel.Token);

            _heartbeatProc = Task.Run(HandleHeartbeat, _cancel.Token);

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
            _heartbeatPoller.Stop();

            await WaitForWorkProceduresToComplete(_workProc, _heartbeatProc, _caughtUpWithStateOfTheWorldProc);

        }
    }
}
