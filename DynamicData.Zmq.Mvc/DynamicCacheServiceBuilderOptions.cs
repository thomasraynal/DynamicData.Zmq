using DynamicData.Zmq.Broker;
using DynamicData.Zmq.Default;
using DynamicData.Zmq.EventCache;
using Microsoft.Extensions.DependencyInjection;
using System;
using DynamicData.Zmq.Serialization;

namespace DynamicData.Zmq.Mvc
{
    public class DynamicCacheServiceBuilderOptions
    {
        public Type EventIdProviderType { get; private set; }
        public Type SerializerType { get; private set; }
        public Type EventCacheType { get; private set; }
        public IServiceCollection ServiceCollection { get; private set; }

        public DynamicCacheServiceBuilderOptions(IServiceCollection serviceCollection)
        {
            SerializerType = typeof(JsonNetSerializer);
            EventIdProviderType = typeof(InMemoryEventIdProvider);
            EventCacheType = typeof(InMemoryEventCache);

            ServiceCollection = serviceCollection;
        }

        public void UseSerializer<TSerializer>() where TSerializer : ISerializer
        {
            SerializerType = typeof(TSerializer);
        }

        public void UseEventIdProvider<TEventIdProvider>() where TEventIdProvider : IEventIdProvider
        {
            EventIdProviderType = typeof(TEventIdProvider);
        }

        public void UseEventCache<TEventCache>() where TEventCache : IEventCache
        {
            EventCacheType = typeof(TEventCache);
        }


    }
}
