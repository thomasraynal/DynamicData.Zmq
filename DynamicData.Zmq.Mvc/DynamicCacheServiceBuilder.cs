﻿using DynamicData.Zmq.Aggregate;
using DynamicData.Zmq.Broker;
using DynamicData.Zmq.Event;
using DynamicData.Zmq.EventCache;
using DynamicData.Zmq.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicData.Zmq.Mvc
{
    public class DynamicCacheServiceBuilder<TKey, TAggregate> where TAggregate : class, IAggregate<TKey>, new()
    {
        public DynamicCacheServiceBuilderOptions Options { get; }

        public DynamicCacheServiceBuilder(DynamicCacheServiceBuilderOptions options)
        {
            Options = options;
        }

        public void Initialize()
        {
            Options.ServiceCollection.AddTransient(typeof(ISerializer), Options.SerializerType);
            Options.ServiceCollection.AddSingleton(typeof(IEventIdProvider), Options.EventIdProviderType);
            Options.ServiceCollection.AddSingleton(typeof(IEventCache), Options.EventCacheType);
            Options.ServiceCollection.AddTransient<IEventSerializer, EventSerializer>();
        }
    }
}
