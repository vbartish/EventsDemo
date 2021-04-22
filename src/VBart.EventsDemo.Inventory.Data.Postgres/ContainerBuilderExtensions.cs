using System;
using Autofac;
using Newtonsoft.Json.Linq;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems;
using VBart.EventsDemo.Inventory.Data.Postgres.DataServices;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Persistence;

namespace VBart.EventsDemo.Inventory.Data.Postgres
{
    public static class ContainerBuilderExtensions
    {
        public static ContainerBuilder AddCdcInboundEventsDataService(this ContainerBuilder builder)
        {
            builder
                .RegisterType<CdcInboundEventDataService>()
                .As<IInboundEventDataService<Guid, JObject>>()
                .InstancePerLifetimeScope();
            return builder;
        }

        public static ContainerBuilder AddCdcCdcProjectionWorkItemDataServiceForAggregate<TAggregate>(this ContainerBuilder builder)
        {
            builder
                .RegisterType<CdcWorkItemDataService<TAggregate>>()
                .As<IWorkItemDataService<CdcProjectionWorkItem>>()
                .InstancePerLifetimeScope();
            return builder;
        }
    }
}