using Autofac;
using VBart.EventsDemo.Inventory.Data.SqlServer.DataServices;

namespace VBart.EventsDemo.Inventory.Data.SqlServer
{
    public static class ContainerBuilderExtensions
    {
        public static ContainerBuilder AddMonolithDataServices(this ContainerBuilder builder)
        {
            builder.RegisterType<VehicleDataService>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<EngineDataService>().AsSelf().InstancePerLifetimeScope();
            return builder;
        }
    }
}