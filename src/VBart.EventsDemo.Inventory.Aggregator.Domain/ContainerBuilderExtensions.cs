using System;
using Autofac;
using Newtonsoft.Json.Linq;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain
{
    public static class ContainerBuilderExtensions
    {
        public static ContainerBuilder AddDefaultInboundPipelineDependencies<TIncomingModel>(this ContainerBuilder builder)
        {
            builder
                .RegisterType<DefaultCdcInboundEventHandler>()
                .As<IInboundEventHandler<Guid, JObject>>()
                .InstancePerLifetimeScope();
            builder
                .RegisterType<SingleAggregateCdcProjectionWorkItemGenerator<TIncomingModel>>()
                .As<IWorkItemGenerator<(InboundEvent<Guid, JObject>, Guid), CdcProjectionWorkItem>>()
                .InstancePerLifetimeScope();
            return builder;
        }
    }
}