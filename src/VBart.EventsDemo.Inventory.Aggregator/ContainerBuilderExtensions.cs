using System;
using Autofac;
using Newtonsoft.Json.Linq;
using VBart.EventsDemo.Inventory.Aggregator.Domain;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Handling;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Projections;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Publishing;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation.AggregateRootKeyExtractors;
using VBart.EventsDemo.Inventory.Data.Postgres;
using VBart.EventsDemo.Inventory.Data.Postgres.DataServices;
using VBart.EventsDemo.Kafka;
using VBart.EventsDemo.Utils;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Handling;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Persistence;
using VBart.EventsDemo.Utils.BackoffProviders;

namespace VBart.EventsDemo.Inventory.Aggregator
{
    public static class ContainerBuilderExtensions
    {
        public static ContainerBuilder AddCdcKafkaBackgroundWorker<TInboundModel, TAggregate>(
            this ContainerBuilder builder,
            Func<IComponentContext, IAggregateRootKeyExtractor<Guid, TInboundModel>> registerAggregateRootExtractor) =>
            builder
                .AddCdcKafkaBackgroundWorker(typeof(TInboundModel).Name.ToUpper(), pipeline =>
                {
                    return pipeline
                        .WithNestedScope(inboundPipelineContainerBuilder =>
                        {
                            inboundPipelineContainerBuilder
                                .AddDefaultInboundPipelineDependencies<TInboundModel>()
                                .AddCdcInboundEventsDataService()
                                .AddCdcCdcProjectionWorkItemDataServiceForAggregate<TAggregate>();
                            inboundPipelineContainerBuilder
                                .Register(context => registerAggregateRootExtractor(context))
                                .As<IAggregateRootKeyExtractor<Guid, TInboundModel>>()
                                .InstancePerLifetimeScope();
                        });
                });

        public static ContainerBuilder AddCdcAggregatorBackgroundWorker<TAggregate>(
            this ContainerBuilder builder,
            string workerName,
            Action<ContainerBuilder> configureProjectionPipeline) =>
            builder
                .AddBackgroundWorker<CdcProjectionWorkItem>(
                    workerName,
                    workerScopeBuilder =>
                    {
                        workerScopeBuilder
                            .RegisterType<CdcInboundEventDataService>()
                            .As<IInboundEventDataService<Guid, JObject>>()
                            .InstancePerLifetimeScope();

                        workerScopeBuilder
                            .RegisterType<CdcWorkItemDataService<TAggregate>>()
                            .As<IWorkItemDataService<CdcProjectionWorkItem>>()
                            .InstancePerLifetimeScope();

                        workerScopeBuilder
                            .RegisterType<OutboxWorkItemsDataService<TAggregate>>()
                            .As<IWorkItemDataService<OutboxWorkItem<Change>>>()
                            .InstancePerLifetimeScope();

                        workerScopeBuilder
                            .AddDefaultBatchWorkItemPipeline<CdcProjectionWorkItem,
                                DefaultBatchWorkItemHandler<CdcProjectionWorkItem>>(
                                null,
                                pipelineScopeBuilder =>
                                {
                                    pipelineScopeBuilder.RegisterOptions<WorkItemProcessingOptions>(workerName);

                                    pipelineScopeBuilder.RegisterType<OutboxWorkItemGenerator>()
                                    .As<IWorkItemGenerator<
                                        (Change PersistedEvent, Guid AggregateUuid),
                                        OutboxWorkItem<Change>>>()
                                    .InstancePerLifetimeScope();

                                    pipelineScopeBuilder
                                        .RegisterType<SourceBasedProjectionHandlerResolver<Guid, JObject>>()
                                        .As<IProjectionHandlerResolver<Guid, JObject>>()
                                        .InstancePerLifetimeScope();
                                    pipelineScopeBuilder
                                        .RegisterType<ExponentialBackoffDelayProvider>()
                                        .As<IBackoffDelayProvider>()
                                        .InstancePerLifetimeScope();
                                    pipelineScopeBuilder
                                    .AddDefaultSingleWorkItemPipeline<CdcProjectionWorkItem,
                                        DefaultCdcWorkItemHandler<Guid, JObject>>(
                                        nestedPipelineScopeBuilder =>
                                        {
                                            nestedPipelineScopeBuilder
                                                .RegisterType<
                                                    BlockingPerAggregateWorkItemHandlingFilter<CdcProjectionWorkItem>>()
                                                .As<IWorkItemHandlingFilter<CdcProjectionWorkItem>>()
                                                .InstancePerLifetimeScope();
                                            return nestedPipelineScopeBuilder;
                                        });

                                    configureProjectionPipeline(pipelineScopeBuilder);

                                    return pipelineScopeBuilder;
                                });
                    });

        public static ContainerBuilder AddOutboxPublisherWorker<TAggregate>(
            this ContainerBuilder builder,
            string workerName)
        {
            return builder.AddBackgroundWorker<OutboxWorkItem<Change>>(
                workerName,
                workerLifetimeScopeBuilder =>
                {
                    workerLifetimeScopeBuilder
                        .AddDefaultBatchWorkItemPipeline<OutboxWorkItem<Change>,
                            DefaultBatchWorkItemHandler<OutboxWorkItem<Change>>>(
                            null,
                            pipelineScopeBuilder =>
                            {
                                pipelineScopeBuilder.RegisterOptions<WorkItemProcessingOptions>(workerName);
                                pipelineScopeBuilder.AddKafkaProducer<string, Change>();
                                pipelineScopeBuilder.AddChangeSerializers();

                                pipelineScopeBuilder
                                    .RegisterType<KafkaChangePublisher>()
                                    .As<IPublisher<Change>>()
                                    .InstancePerLifetimeScope();

                                pipelineScopeBuilder
                                    .RegisterType<ExponentialBackoffDelayProvider>()
                                    .As<IBackoffDelayProvider>()
                                    .InstancePerLifetimeScope();
                                pipelineScopeBuilder
                                    .AddDefaultSingleWorkItemPipeline<OutboxWorkItem<Change>,
                                        DefaultOutboxWorkItemHandler<Change>>(
                                        nestedPipelineScopeBuilder =>
                                        {
                                            nestedPipelineScopeBuilder
                                                .RegisterType<
                                                    BlockingPerAggregateWorkItemHandlingFilter<CdcProjectionWorkItem>>()
                                                .As<IWorkItemHandlingFilter<CdcProjectionWorkItem>>()
                                                .InstancePerLifetimeScope();
                                            return nestedPipelineScopeBuilder;
                                        });
                                return pipelineScopeBuilder;
                            });
                    workerLifetimeScopeBuilder
                        .RegisterType<OutboxWorkItemsDataService<TAggregate>>()
                        .As<IWorkItemDataService<OutboxWorkItem<Change>>>()
                        .InstancePerLifetimeScope();
                });
        }
    }
}