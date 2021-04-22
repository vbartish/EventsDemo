using System;
using System.Collections.Generic;
using Autofac;
using Confluent.Kafka;
using Humanizer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;
using VBart.EventsDemo.Utils;

namespace VBart.EventsDemo.Kafka
{
    public static class KafkaExtensions
    {
        public static ContainerBuilder AddCdcKafkaBackgroundWorker(
            this ContainerBuilder builder,
            string name,
            Func<Pipeline<InboundEvent<Guid, JObject>>, Pipeline<InboundEvent<Guid, JObject>>> configureInboundEventProcessingPipeline)
        {
            builder.Register(context =>
                    new KafkaBackgroundWorker<Guid, JObject>(
                        name,
                        context
                            .Resolve<ILifetimeScope>()
                            .BeginLifetimeScope(
                                childScopeBuilder =>
                                {
                                    childScopeBuilder
                                        .AddDebeziumSerializers()
                                        .RegisterOptions<KafkaConsumerOptions>(name, registration => registration.InstancePerLifetimeScope())
                                        .AddKafkaConsumer<Guid, JObject>(name)
                                        .AddDefaultCdcKafkaConsumptionPipeline()
                                        .AddDebeziumMessageHandler(configureInboundEventProcessingPipeline);
                                })))
                .As<IHostedService>()
                .SingleInstance();
            return builder;
        }

        public static ContainerBuilder AddDefaultCdcKafkaConsumptionPipeline(this ContainerBuilder containerBuilder) =>
            containerBuilder.AddKafkaConsumptionPipeline<Guid>(_ =>
            {
                /* TODO: add policies and steps */
            });

        public static ContainerBuilder AddKafkaConsumptionPipeline<TKey>(
            this ContainerBuilder containerBuilder,
            Action<Pipeline<ConsumeResult<TKey, JObject>>> configureConsumerPipeline)
        {
            containerBuilder.Register(context =>
            {
                var pipe = new Pipeline<ConsumeResult<TKey, JObject>>(context.Resolve<ILifetimeScope>().BeginLifetimeScope());
                configureConsumerPipeline(pipe);
                return pipe;
            });
            return containerBuilder;
        }

        public static ContainerBuilder AddDebeziumMessageHandler<TKey>(
            this ContainerBuilder builder,
            Func<Pipeline<InboundEvent<TKey, JObject>>, Pipeline<InboundEvent<TKey, JObject>>> configureInboundEventProcessingPipeline)
        {
            builder
                .Register(context =>
                {
                    var pipeline = new Pipeline<InboundEvent<TKey, JObject>>(context.Resolve<ILifetimeScope>().BeginLifetimeScope());
                    return configureInboundEventProcessingPipeline(pipeline);
                })
                .As<Pipeline<InboundEvent<TKey, JObject>>>()
                .InstancePerLifetimeScope();
            builder
                .RegisterType<DebeziumMessageHandler<TKey>>()
                .As<IKafkaMessageHandler<TKey, JObject>>()
                .InstancePerLifetimeScope();

            return builder;
        }

        public static ContainerBuilder AddDebeziumSerializers(this ContainerBuilder builder)
        {
            builder
                .RegisterType<JsonDeserializer>()
                .As<IDeserializer<JObject>>()
                .InstancePerLifetimeScope();
            builder
                .RegisterType<DbKeyDeserializer>()
                .As<IDeserializer<Guid>>()
                .InstancePerLifetimeScope();
            return builder;
        }

        public static ContainerBuilder AddKafkaConsumer<TKey, TValue>(
            this ContainerBuilder containerBuilder,
            string name)
        {
            containerBuilder.Register(context =>
                {
                    var options = context.Resolve<KafkaConsumerOptions>();
                    var loggerFactory = context.Resolve<ILoggerFactory>();
                    var logCategoryName = $"kafka-consumer-{name.ToLower().Kebaberize()}";
                    var consumerBuilder =
                        new ConsumerBuilder<TKey, TValue>(options.ToConfluentConfiguration())
                            .SetLogHandler((_, message) => HandleKafkaLog(message, loggerFactory, logCategoryName))
                            .SetErrorHandler((_, error) => HandleKafkaErrorLog(error, loggerFactory, logCategoryName))
                            .SetStatisticsHandler((_, statistics) => HandleKafkaStatisticsLog(statistics, loggerFactory, logCategoryName))
                            .SetPartitionsAssignedHandler((_, assignedPartitions) =>
                                HandleKafkaAssignedPartitionsLog(assignedPartitions, loggerFactory, logCategoryName))
                            .SetPartitionsRevokedHandler((_, revokedPartitions) =>
                                HandleKafkaRevokedPartitionsLog(revokedPartitions, loggerFactory, logCategoryName));

                    var valueDeserializer = context.ResolveOptional<IDeserializer<TValue>>();
                    if (valueDeserializer != null)
                    {
                        consumerBuilder = consumerBuilder.SetValueDeserializer(valueDeserializer);
                    }

                    var keyDeserializer = context.ResolveOptional<IDeserializer<TKey>>();
                    if (keyDeserializer != null)
                    {
                        consumerBuilder = consumerBuilder.SetKeyDeserializer(keyDeserializer);
                    }

                    return consumerBuilder.Build();
                })
                .As<IConsumer<TKey, TValue>>().InstancePerLifetimeScope();
            return containerBuilder;
        }

        private static void HandleKafkaLog(
            LogMessage message,
            ILoggerFactory loggerFactory,
            string logCategoryName)
        {
            var logger = loggerFactory.CreateLogger(logCategoryName);
            switch (message.Level)
            {
                case SyslogLevel.Emergency:
                    logger.LogCritical(message.Message);
                    break;
                case SyslogLevel.Alert:
                    logger.LogError(message.Message);
                    break;
                case SyslogLevel.Critical:
                    logger.LogCritical(message.Message);
                    break;
                case SyslogLevel.Error:
                    logger.LogError(message.Message);
                    break;
                case SyslogLevel.Warning:
                    logger.LogWarning(message.Message);
                    break;
                case SyslogLevel.Notice:
                    logger.LogInformation(message.Message);
                    break;
                case SyslogLevel.Info:
                    logger.LogInformation(message.Message);
                    break;
                case SyslogLevel.Debug:
                    logger.LogDebug(message.Message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(message.Level));
            }
        }

        private static void HandleKafkaStatisticsLog(
            string statistics,
            ILoggerFactory loggerFactory,
            string logCategoryName)
        {
            var logger = loggerFactory.CreateLogger(logCategoryName);
            logger.LogDebug($"Kafka statistics: {statistics}");
        }

        private static void HandleKafkaAssignedPartitionsLog(
            List<TopicPartition> assignedPartitions,
            ILoggerFactory loggerFactory,
            string logCategoryName)
        {
            var logger = loggerFactory.CreateLogger(logCategoryName);
            logger.LogDebug(
                $"Kafka consumer got partitions assigned: {string.Join(",", assignedPartitions)}");
        }

        private static void HandleKafkaRevokedPartitionsLog(
            List<TopicPartitionOffset> revokedPartitions,
            ILoggerFactory loggerFactory,
            string logCategoryName)
        {
            var logger = loggerFactory.CreateLogger(logCategoryName);
            logger.LogDebug(
                $"Kafka consumer got partitions revoked: {string.Join(",", revokedPartitions)}");
        }

        private static void HandleKafkaErrorLog(Error error,
            ILoggerFactory loggerFactory,
            string logCategoryName)
        {
            var logger = loggerFactory.CreateLogger(logCategoryName);
            logger.LogError($"Kafka error ({error.Code}): {error.Reason}");
        }
    }
}