# DynamicData.Zmq

[![Build status](https://ci.appveyor.com/api/projects/status/ac9e3j57b65ove3p?svg=true)](https://ci.appveyor.com/project/thomasraynal/dynamicdata-zmq)

DynamicData.Zmq is a classic ZeroMQ (using [NetMQ](https://github.com/zeromq/netmq)) pub/sub platform using a broker for message control. Read operations on the system state are done on a local cache while write operations are done at the broker level. The broker itself act as an event database.

Each producer generates events or commands on a given subject (i.e, a currency pair price). The subject itself is as such a stream of events, that is an aggregate. Each event is defined by routable properties which serialize as a ZeroMQ readable subject (i.e, my event should be serialized as {CcyPair}.{Market} or {CcyPair}.{Market}.{Counterparty} in order to allow a ZeroMQ filter at the socket level).

The broker captures the event, gives it an id and a position in the stream, and save it before forwarding it to its subscribers.

The subscriber holds a local cache, using [DynamicData](https://github.com/RolandPheasant/DynamicData), where events are applied on aggregates (i.e, a ChangePriceEvent on the EUR/USD currency pair). DynamicData ObservableCache allows then to process the event to internal subscribers, using Rx.

The subscriber manages disconnection and cache rebuilding. The broker keeps a "state of the world" router socket open to get the current application state which is then reconciled locally with the events received during the caughting-up process. 

[The E2E tests give a bird eye view of the process](https://github.com/thomasraynal/DynamicData.Zmq/tree/master/DynamicData.Zmq.Tests.E2E)

DynamicData.Zmq exposes a .NET Core MVC API (here using [Scrutor](https://github.com/khellang/Scrutor) for DI), but could be used withing any .NET application.

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

     services.AddMvc();


