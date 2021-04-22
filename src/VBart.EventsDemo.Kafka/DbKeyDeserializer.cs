using System;
using System.Linq;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;

namespace VBart.EventsDemo.Kafka
{
    public class DbKeyDeserializer : JsonDeserializer, IDeserializer<Guid>
    {
        private readonly ILogger<DbKeyDeserializer> _logger;

        public DbKeyDeserializer(ILogger<DbKeyDeserializer> logger)
        {
            _logger = logger;
        }

        Guid IDeserializer<Guid>.Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            if (isNull)
            {
                return Guid.Empty;
            }

            var deserialized = Deserialize(data, isNull, context);

            if (deserialized == null)
            {
                _logger.LogWarning(
                    $"Unexpected null value after deserializing JSON.{Encoding.UTF8.GetString(data.ToArray())})");
                return Guid.Empty;
            }

            var payload = deserialized.GetValue(Constants.DebeziumMessagePayloadKey, StringComparison.OrdinalIgnoreCase);

            if (payload == null)
            {
                _logger.LogWarning($"Could not locate message payload. Message: {Encoding.UTF8.GetString(data.ToArray())}");
                return Guid.Empty;
            }

            try
            {
                return Guid.Parse(payload
                    .Children()
                    .Single()
                    .Values<string>()
                    .Single()!);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentNullException)
            {
                _logger.LogWarning(ex, $"Could not locate db key in the payload. Message: {Encoding.UTF8.GetString(data.ToArray())}");
                return Guid.Empty;
            }
        }
    }
}