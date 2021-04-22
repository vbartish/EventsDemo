using System;
using Autofac;
using VBart.EventsDemo.Inventory.Aggregator.Domain;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation.AggregateRootKeyExtractors;
using VBart.EventsDemo.Inventory.Data.Postgres;
using VBart.EventsDemo.Kafka;

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
    }
}