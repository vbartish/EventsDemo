using System;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using VBart.EventsDemo.Inventory.Aggregator.Domain;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Projections;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation.AggregateRootKeyExtractors;
using VBart.EventsDemo.Inventory.Data;
using VBart.EventsDemo.Inventory.Data.Postgres.DataServices;
using VBart.EventsDemo.Inventory.DataModels;
using VBart.EventsDemo.Utils;
using Vehicle = VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates.Vehicle;

namespace VBart.EventsDemo.Inventory.Aggregator
{
    static class Program
    {
        private const int DefaultPort = 50051;

        static Task Main() =>
            new HostBuilder()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", true, true);
                    config.AddEnvironmentVariables();
                })
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureServices(services =>
                {
                    services.AddGrpc();
                    services.AddGrpcReflection();
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddConfiguration(context.Configuration);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel((context, options) =>
                        {
                            var myServiceUri = context.Configuration.GetServiceUri("InventoryAggregator");
                            options.ListenLocalhost(myServiceUri?.Port ?? DefaultPort,
                                listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
                        });
                    webBuilder.Configure((hostBuilderContext, appBuilder) =>
                    {
                        appBuilder
                            .UseRouting()
                            .UseEndpoints(endpoints =>
                            {
                                // TODO: add once ready with compensation API
                                // endpoints.MapGrpcService<CompensationService>();
                                endpoints.MapGet("/",
                                    async context =>
                                    {
                                        await context.Response.WriteAsync(
                                            "Communication with gRPC endpoints must be made through a gRPC client.");
                                    });

                                if (hostBuilderContext.HostingEnvironment.IsDevelopment())
                                {
                                    endpoints.MapGrpcReflectionService();
                                }
                            });
                    });
                })
                .ConfigureContainer<ContainerBuilder>((hostBuilderContext, containerBuilder) =>
                {
                    containerBuilder
                        .AddTypeRegistry()
                        .AddPostgresInfrastructure(() =>
                            hostBuilderContext.Configuration.GetConnectionString("aggregatordb", "Connection")
                            ?? (hostBuilderContext.Configuration["DevConnectionString"] ??
                                throw new InvalidOperationException("Connection string not defined.")))
                        .AddCdcKafkaBackgroundWorker<Engine, Vehicle>(_ => new EngineAggregateRootKeyExtractor())
                        .AddCdcKafkaBackgroundWorker<DataModels.Vehicle, Vehicle>(_ =>
                            new VehicleAggregateRootKeyExtractor())
                        .AddCdcAggregatorBackgroundWorker<Vehicle>("VEHICLE_AGGREGATOR", projectionPipelineBuilder =>
                        {
                            projectionPipelineBuilder.RegisterType<EngineProjectionHandler>()
                                .Keyed<IProjectionHandler<Guid, JObject>>("dbServer.dbo.Engines")
                                .InstancePerLifetimeScope();
                            projectionPipelineBuilder.RegisterType<VehicleProjectionHandler>()
                                .Keyed<IProjectionHandler<Guid, JObject>>("dbServer.dbo.Vehicles")
                                .InstancePerLifetimeScope();
                            projectionPipelineBuilder
                                .RegisterType<AggregateDataService<Vehicle, MetaData>>()
                                .As<IAggregateDataService<Vehicle, MetaData>>()
                                .As<IAggregateSnapshotDataService<Vehicle, MetaData>>()
                                .InstancePerLifetimeScope();
                            projectionPipelineBuilder
                                .RegisterType<OutboxWorkItemsDataService<Change>>()
                                .InstancePerLifetimeScope();
                        })
                        .AddOutboxPublisherWorker<Vehicle>("VEHICLE_PUBLISHER");;
                        containerBuilder.RegisterType<SystemClock>().As<ISystemClock>().SingleInstance();
                })
                .Build()
                .RunAsync();
    }
}