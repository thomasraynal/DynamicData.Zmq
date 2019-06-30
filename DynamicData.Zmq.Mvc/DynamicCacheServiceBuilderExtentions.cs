using DynamicData.Zmq.Aggregate;
using DynamicData.Zmq.Broker;
using DynamicData.Zmq.Cache;
using DynamicData.Zmq.Producer;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace DynamicData.Zmq.Mvc
{
    public static class DynamicCacheServiceBuilderExtentions
    {
        public static DynamicCacheServiceBuilder<TKey, TAggregate> AddDynamicCacheService<TKey, TAggregate>(this IServiceCollection services, Action<DynamicCacheServiceBuilderOptions> cacheServiceConfiguration)
            where TAggregate : class, IAggregate<TKey>, new()
        {

            var options = new DynamicCacheServiceBuilderOptions(services);
            cacheServiceConfiguration(options);

            var builder = new DynamicCacheServiceBuilder<TKey, TAggregate>(options);

            builder.Initialize();

            return builder;
        }

        public static DynamicCacheServiceBuilder<TKey, TAggregate> AddDynamicCache<TKey, TAggregate>(this DynamicCacheServiceBuilder<TKey, TAggregate> builder, Action<IDynamicCacheConfiguration> cacheConfigurationBuilder)
            where TAggregate : class, IAggregate<TKey>, new()
        {
            var configuration = new DynamicCacheConfiguration();
            cacheConfigurationBuilder(configuration);

            builder.Options.ServiceCollection.AddSingleton<IDynamicCacheConfiguration>(configuration);
            builder.Options.ServiceCollection.AddSingleton<IDynamicCache<TKey, TAggregate>, DynamicCache<TKey, TAggregate>>();

            return builder;
        }

        public static DynamicCacheServiceBuilder<TKey, TAggregate> AddBroker<TKey, TAggregate>(this DynamicCacheServiceBuilder<TKey, TAggregate> builder, Action<IBrokerageServiceConfiguration> brokerConfigurationBuilder)
           where TAggregate : class, IAggregate<TKey>, new()
        {
            var configuration = new BrokerageServiceConfiguration();
            brokerConfigurationBuilder(configuration);

            builder.Options.ServiceCollection.AddSingleton<IBrokerageServiceConfiguration>(configuration);

            builder.Options.ServiceCollection.AddSingleton<IBrokerageService, BrokerageService>();

            return builder;
        }

        public static DynamicCacheServiceBuilder<TKey, TAggregate> AddProducer<TKey, TAggregate, TProducer, TConfiguration>(this DynamicCacheServiceBuilder<TKey, TAggregate> builder, Action<TConfiguration> producerConfigurationBuilder)
            where TAggregate : class, IAggregate<TKey>, new()
            where TProducer : class, IProducer<TKey, TAggregate>
            where TConfiguration : class, IProducerConfiguration, new()
        {
            var configuration = new TConfiguration();
            producerConfigurationBuilder(configuration);

            builder.Options.ServiceCollection.AddSingleton(configuration);

            builder.Options.ServiceCollection.AddSingleton<IProducer<TKey, TAggregate>, TProducer>();

            return builder;
        }

    }
}
