using System;
using Autofac;
using Autofac.Builder;
using Humanizer;
using Microsoft.Extensions.Configuration;

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
    }
}