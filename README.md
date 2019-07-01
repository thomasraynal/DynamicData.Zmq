# DynamicData.Zmq

[![Build status](https://ci.appveyor.com/api/projects/status/ac9e3j57b65ove3p?svg=true)](https://ci.appveyor.com/project/thomasraynal/dynamicdata-zmq) [![NuGet](https://img.shields.io/nuget/v/DynamicData.Zmq.svg)](https://www.nuget.org/packages/DynamicData.Zmq)
[![NuGet](https://img.shields.io/nuget/v/DynamicData.Zmq.Mvc.svg)](https://www.nuget.org/packages/DynamicData.Zmq.Mvc)

DynamicData.Zmq is a classic ZeroMQ (using [NetMQ](https://github.com/zeromq/netmq)) pub/sub platform using a broker for message control. Read operations on the system state are done on a local cache while write operations are done at the broker level. The broker itself act as an event database.

Each producer generates events or commands on a given subject (e.g, price changed event for a currency pair). The subject itself is as such a stream of events, that is an aggregate. 

```csharp
    public class CurrencyPair : AggregateBase<string>
    {

        public CcyPairState State { get; set; }

        public double Ask { get; set; }
        public double Bid { get; set; }
        public double Mid { get; set; }
        public double Spread { get; set; }

        public override string ToString()
        {
            return $"{this.Id}({AppliedEvents.Count()} event(s))";
        }
    }

    public interface IAggregate
    {
        IEnumerable<IEvent> AppliedEvents { get; }
        void Apply(IEvent @event);
    }

    public interface IAggregate<TKey> : IAggregate
    {
        TKey Id { get; set; }
        int Version { get; set; }
        void Apply<TAggregate>(IEvent<TKey, TAggregate> @event) where TAggregate : IAggregate<TKey>;
    }

```

Each event is defined by routable properties which serialize as a ZeroMQ readable subject (e.g, my event should be serialized as {CcyPair}.{Market} or {CcyPair}.{Market}.{Counterparty}), with the aggregate id always in the first position) in order to allow a ZeroMQ filter at the socket level.

```csharp
    public class ChangeCcyPairPrice : CommandBase<string, CurrencyPair>
    {
        public ChangeCcyPairPrice(string ccyPairId, string market, double ask, double bid, double mid, double spread): base(ccyPairId)
        {
            Ask = ask;
            Bid = bid;
            Mid = mid;
            Spread = spread;
            Market = market;
        }

        public double Ask { get; set; }
        public double Bid { get; set; }
        public double Mid { get; set; }
        public double Spread { get; set; }

        [RoutingPosition(0)]
        public string Market { get; set; }

        public override void Apply(CurrencyPair aggregate)
        {
            aggregate.Ask = Ask;
            aggregate.Bid = Bid;
            aggregate.Mid = Mid;
            aggregate.Spread = Spread;
        }
    }
```

The broker captures the event, gives it an id and a position in the stream, and save it before forwarding it to its subscribers.

```csharp

    public interface IEventCache
    {
        Task<IEventId> AppendToStream(string subject, byte[] payload);
        Task<IEnumerable<IEventMessage>> GetAllStreams();
        Task<IEnumerable<IEventMessage>> GetStream(string streamId);
        Task<IEnumerable<IEventMessage>> GetStreamBySubject(string subject);
        Task Clear();
    }

    public interface IEventIdProvider
    {
        Task Reset();
        IEventId Next(string streamName, string subject);
    }}

```

The subscriber holds a local cache, using [DynamicData](https://github.com/RolandPheasant/DynamicData), where events are applied on aggregates (e.g, a ChangePriceEvent on the EUR/USD currency pair). DynamicData ObservableCache allows then to process the changes to internal subscribers, using Rx.

```csharp

    public interface IDynamicCache<TKey, TAggregate> : IActor where TAggregate : IAggregate<TKey>
    {
        IObservableCache<TAggregate, TKey> OnItemChanged { get; }
        IEnumerable<TAggregate> Items { get; }
        IObservable<DynamicCacheState> OnStateChanged { get; }
        DynamicCacheState CacheState { get; }
        bool IsStaled { get; }
        IObservable<bool> OnStaled { get; }
        bool IsCaughtingUp { get; }
        IObservable<bool> OnCaughtingUp { get; }

    }

```

The subscriber manages disconnection and cache rebuilding. The broker keeps a "state of the world" router socket open to get the current application state which is then reconciled locally with the events received during the caughting-up process. 

[The E2E tests give a bird eye view of the process.](https://github.com/thomasraynal/DynamicData.Zmq/tree/master/DynamicData.Zmq.Tests.E2E)

The [BrokerageService](https://github.com/thomasraynal/DynamicData.Zmq/blob/master/DynamicData.Zmq/Broker/BrokerageService.cs) and [DynamicCache](https://github.com/thomasraynal/DynamicData.Zmq/blob/master/DynamicData.Zmq/Cache/DynamicCache.cs) class hold the core of the logic explained above.

DynamicData.Zmq exposes a [.NET Core MVC API](https://www.nuget.org/packages/DynamicData.Zmq.Mvc)  (here using [Scrutor](https://github.com/khellang/Scrutor) for DI), but could be used withing any .NET application.

```csharp
var dynamicCacheBuilder = services.AddDynamicCacheService<string, CurrencyPair>(configuration =>
	{
            configuration.UseEventCache<InMemoryEventCache>();
            configuration.UseEventIdProvider<InMemoryEventIdProvider>();
            configuration.UseSerializer<JsonNetSerializer>();
	});

    dynamicCacheBuilder
			.AddDynamicCache(configuration =>
                            {
                                configuration.HeartbeatEndpoint = HeartbeatEndpoint;
                                configuration.StateOfTheWorldEndpoint = StateOfTheWorldEndpoint;
                                configuration.SubscriptionEndpoint = ToSubscribersEndpoint;
                            })
                        .AddBroker(configuration =>
                            {
                                configuration.HeartbeatEndpoint = HeartbeatEndpoint;
                                configuration.StateOfTheWorldEndpoint = StateOfTheWorldEndpoint;
                                configuration.ToSubscribersEndpoint = ToSubscribersEndpoint;
                                configuration.ToPublisherEndpoint = ToPublishersEndpoint;
                            });


     dynamicCacheBuilder.AddProducer<string, CurrencyPair, Market, MarketConfiguration>(configuration =>
              {
                  configuration.HeartbeatEndpoint = HeartbeatEndpoint;
                  configuration.BrokerEndpoint = ToPublishersEndpoint;
              });


     services.Scan(scan => scan.FromEntryAssembly()
                               .AddClasses(classes => classes.AssignableTo<IHostedService>())
                               .AsImplementedInterfaces());

```

Then, host it in your .NET Core MVC application.

```csharp

 public class HostCache : IHostedService
    {
        private readonly IDynamicCache<string, CurrencyPair> _cache;
        private readonly ILogger<HostCache> _logger;

        public HostCache(ILogger<HostCache> logger, IDynamicCache<string, CurrencyPair> cache)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cache.OnItemChanged
                  .Connect()
                  .ToCollection()
                  .Scan(CcyPairPrices.Default, (previous, obs) =>
                    {
                        var prices = new CcyPairPrices();

                        foreach (var o in obs)
                        {
                            prices.Prices.Add(new Price()
                            {
                                CcyPair = o.Id,
                                Ask = o.Ask,
                                Bid = o.Bid,
                                EventCount = o.AppliedEvents.Count()
                            });
                        }

                        return prices;

                    })
                  .Subscribe(state =>
                {
                    _logger.LogInformation(state.ToString());
                }
        );


            await _cache.Run();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _cache.Destroy();
        }

```