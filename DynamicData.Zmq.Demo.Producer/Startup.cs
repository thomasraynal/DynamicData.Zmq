using DynamicData.Zmq.Default;
using DynamicData.Zmq.Demo;
using DynamicData.Zmq.Dto;
using DynamicData.Zmq.EventCache;
using DynamicData.Zmq.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Zmq.Demo
{
    public class Startup
    {
        public readonly string ToPublishersEndpoint = "tcp://localhost:8080";
        public readonly string ToSubscribersEndpoint = "tcp://localhost:8181";
        public readonly string HeartbeatEndpoint = "tcp://localhost:8282";
        public readonly string StateOfTheWorldEndpoint = "tcp://localhost:8383";

        public void ConfigureServices(IServiceCollection services)
        {

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

        }

        public void Configure(IApplicationBuilder app, Microsoft.AspNetCore.Hosting.IHostingEnvironment env)
        {
            JsonConvert.DefaultSettings = () =>
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    TypeNameHandling = TypeNameHandling.Objects,
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };

                settings.Converters.Add(new AbstractConverter<IEventMessage, EventMessage>());
                settings.Converters.Add(new AbstractConverter<IProducerMessage, ProducerMessage>());
                settings.Converters.Add(new AbstractConverter<IEventId, EventId>());
                settings.Converters.Add(new AbstractConverter<IStateReply, StateReply>());
                settings.Converters.Add(new AbstractConverter<IStateRequest, StateRequest>());

                return settings;
            };

            app.UseMvc();
        }
    }
}
