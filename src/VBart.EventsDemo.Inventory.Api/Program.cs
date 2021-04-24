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
using VBart.EventsDemo.Inventory.Api.AutoMapper;
using VBart.EventsDemo.Inventory.Data;
using VBart.EventsDemo.Inventory.Data.SqlServer;

namespace VBart.EventsDemo.Inventory.Api
{
    static class Program
    {
        static Task Main() =>
            new HostBuilder()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;

                    config.AddJsonFile("appsettings.json", true, true);
                    config.AddEnvironmentVariables();
                })
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureServices(services =>
                {
                    services.AddGrpc();
                    services.AddGrpcReflection();
                    services.AddAutoMapper(config => config.AddProfile<InventoryProfile>());
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureKestrel((context, options) =>
                        {
                            options.ConfigureEndpointDefaults(listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
                        })
                        .Configure((_, appBuilder) =>
                        {
                            appBuilder
                                .UseRouting()
                                .UseEndpoints(endpoints =>
                                {
                                    endpoints.MapGrpcService<InventoryService>();
                                    endpoints.MapGet("/",
                                        async context =>
                                        {
                                            await context.Response.WriteAsync(
                                                "Communication with gRPC endpoints must be made through a gRPC client.");
                                        });
                                });
                        });
                })
                .ConfigureContainer<ContainerBuilder>((hostBuilderContext, containerBuilder) =>
                {
                    containerBuilder.AddSqlServerInfrastructure(() =>
                        hostBuilderContext.Configuration.GetConnectionString("monolithicdb", "Connection")
                        ?? (hostBuilderContext.Configuration["DevConnectionString"] ??
                            throw new InvalidOperationException("Connection string not defined.")));
                    containerBuilder.AddMonolithDataServices();
                })
                .Build()
                .RunAsync();
    }
}