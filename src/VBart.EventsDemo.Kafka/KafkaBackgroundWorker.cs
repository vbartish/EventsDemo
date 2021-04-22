using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VBart.EventsDemo.Utils;

namespace VBart.EventsDemo.Kafka
{
    public class KafkaBackgroundWorker<TKey, TValue> : BackgroundService
    {
        private readonly string _name;
        private readonly ILogger<KafkaBackgroundWorker<TKey, TValue>> _logger;
        private readonly Pipeline<ConsumeResult<TKey, TValue>> _pipeline;
        private readonly IConsumer<TKey, TValue> _consumer;
        private readonly KafkaConsumerOptions _options;

        public KafkaBackgroundWorker(string name, ILifetimeScope scope)
        {
            _name = name;
            _logger = scope.Resolve<ILogger<KafkaBackgroundWorker<TKey, TValue>>>();
            _pipeline = scope.Resolve<Pipeline<ConsumeResult<TKey, TValue>>>();
            _consumer = scope.Resolve<IConsumer<TKey, TValue>>();
            _options = scope.Resolve<KafkaConsumerOptions>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Starting consumer: {_name}");

            _consumer.Subscribe(_options.TopicsList);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(stoppingToken);
                    if (result == null || result.IsPartitionEOF)
                    {
                        continue;
                    }

                    await HandleConsumedResult(result);
                    _consumer.Commit();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Kafka consumption was cancelled.");
                }
                catch (ConsumeException exception) when (!exception.Error.IsFatal)
                {
                    _logger.LogError(exception, "Consume error for message.");
                }
                catch (KafkaMessageHandlingException exception)
                {
                    _logger.LogError(exception, "Handle error for message.");
                }
                catch (KafkaException exception)
                {
                    _logger.LogError(exception, "Exception on committing offsets.");
                }
                catch (Exception exception)
                {
                    _logger.LogCritical(exception, "Unexpected exception on kafka topic consumption. Consumer worker will be shut down.");
                    break;
                }
            }

            _logger.LogInformation($"Stopping consumer: {_name}");
        }

        private async Task HandleConsumedResult(ConsumeResult<TKey, TValue> result)
        {
            await _pipeline.Do(result, async scope =>
            {
                await scope.Resolve<IKafkaMessageHandler<TKey, TValue>>().Handle(result);
            });
        }
    }
}