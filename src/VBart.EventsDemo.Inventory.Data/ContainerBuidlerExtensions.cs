using System;
using System.Data.Common;
using Autofac;
using Microsoft.Data.SqlClient;

namespace VBart.EventsDemo.Inventory.Data
{
    public static class ContainerBuilderExtensions
    {
        public static ContainerBuilder AddSqlServerInfrastructure(this ContainerBuilder builder, Func<string> getConnectionString)
        {
            builder.Register(_ => SqlClientFactory.Instance).As<DbProviderFactory>().SingleInstance();
            builder.Register(_ => new SqlConnectionStringBuilder(getConnectionString())).As<DbConnectionStringBuilder>().SingleInstance();
            builder.RegisterType<DbAdapter>().As<IDbAdapter>().InstancePerLifetimeScope();
            return builder;
        }
    }
}