using System;
using System.Collections.Immutable;
using Autofac;
using Autofac.Builder;
using Humanizer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using VBart.EventsDemo.Utils.BackgroundProcessing;
using VBart.EventsDemo.Utils.BackgroundProcessing.Options;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Handling;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Persistence;

namespace VBart.EventsDemo.Utils
{
    public static class ContainerBuilderExtensions
    {
        public static ContainerBuilder RegisterOptions<TOptions>(this ContainerBuilder builder,
            string? name = null,
            Action<IRegistrationBuilder<TOptions, SimpleActivatorData, SingleRegistrationStyle>>? configureRegistration = null)
            where TOptions : notnull, new()
        {
            var sectionKey = string.IsNullOrWhiteSpace(name)
                ? $"{typeof(TOptions).Name.Underscore().ToUpper()}"
                : $"{name.Underscore().ToUpper()}:{typeof(TOptions).Name.Underscore().ToUpper()}";

            var registrationBuilder = builder
                .Register(context =>
                {
                    var options = new TOptions();
                    var configuration = context.Resolve<IConfiguration>();
                    configuration.GetSection(sectionKey).Bind(options);
                    return options;
                })
                .AsSelf();

            if (configureRegistration == null)
            {
                registrationBuilder.InstancePerLifetimeScope();
                return builder;
            }

            configureRegistration.Invoke(registrationBuilder);

            return builder;
        }



        public static ContainerBuilder AddBackgroundWorker<TWorkItem>(
            this ContainerBuilder builder,
            string name,
            Action<ContainerBuilder> configureWorkerLifetimeScope)
            where TWorkItem : class, IWorkItem
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.RegisterOptions<BackgroundWorkerOptions>(name);
            builder
                .Register(
                    c => new BackgroundWorker<TWorkItem>(
                        name,
                        c.Resolve<ILifetimeScope>().BeginLifetimeScope(configureWorkerLifetimeScope)))
                .As<IHostedService>()
                .SingleInstance();
            return builder;
        }

        public static ContainerBuilder AddDefaultSingleWorkItemPipeline<TSingleWorkItem, TSingleWorkItemHandler>(
            this ContainerBuilder builder,
            Func<ContainerBuilder, ContainerBuilder>? buildNestedScope = null)
            where TSingleWorkItem : class, IWorkItem
            where TSingleWorkItemHandler : IWorkItemHandler<TSingleWorkItem>
        {
            builder
                .Register(context =>
                    new Pipeline<TSingleWorkItem>(context
                            .Resolve<ILifetimeScope>()
                            .BeginLifetimeScope(nestedScopeBuilder => buildNestedScope?.Invoke(nestedScopeBuilder)))
                        .WithNewAmbientTransaction())
                .AsSelf()
                .InstancePerLifetimeScope();
            builder
                .RegisterType<TSingleWorkItemHandler>()
                .As<IWorkItemHandler<TSingleWorkItem>>()
                .InstancePerLifetimeScope();
            return builder;
        }

        public static ContainerBuilder AddDefaultBatchWorkItemPipeline
            <TSingleWorkItem, TWorkItemBatchHandler>(
            this ContainerBuilder builder,
            string? name = null,
            Func<ContainerBuilder, ContainerBuilder>? buildNestedScope = null)
            where TSingleWorkItem : class, IWorkItem
            where TWorkItemBatchHandler : IWorkItemBatchHandler<TSingleWorkItem>
        {
            builder
                .Register(
                c => new Pipeline<WorkItemBatchContext<TSingleWorkItem>>(c
                        .Resolve<ILifetimeScope>()
                        .BeginLifetimeScope(nestedScopeBuilder =>
                        {
                            nestedScopeBuilder
                                .RegisterType<TWorkItemBatchHandler>()
                                .As<IWorkItemBatchHandler<TSingleWorkItem>>()
                                .InstancePerLifetimeScope();
                            buildNestedScope?.Invoke(nestedScopeBuilder);
                        }))
                    .WithRetryForever()
                    .WithNewAmbientTransaction()
                    .InWorkItemsBatchContext(async scope =>
                    {
                        var options = scope.Resolve<BackgroundWorkerOptions>();
                        var workItemDataService = scope.Resolve<IWorkItemDataService<TSingleWorkItem>>();
                        var batchedWorkItems =
                            await workItemDataService.PickABatchWithDistroLock(options.WorkItemsBatchSize, options.ScaleFactor);
                        return new BatchWorkItem<TSingleWorkItem>
                        {
                            Batch = batchedWorkItems.ToImmutableList()
                        };
                    }))
                .AsSelf()
                .InstancePerLifetimeScope();
            return builder;
        }
    }
}