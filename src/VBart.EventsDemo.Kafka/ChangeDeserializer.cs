using System;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates;

namespace VBart.EventsDemo.Kafka
{
    internal class ChangeDeserializer : IDeserializer<Change>
    {
        private readonly ILogger<ChangeDeserializer> _logger;

        public ChangeDeserializer(ILogger<ChangeDeserializer> logger)
        {
            _logger = logger;
        }

        public Change? Deserialize(
            ReadOnlySpan<byte> data,
            bool isNull,
            SerializationContext context)
        {
            try
            {
                return isNull ? null : Change.Parser.ParseFrom(data.ToArray());
            }
            catch (Exception exception)
            {
                _logger.LogError( exception, $"Message deserialization error (topic: {context.Topic}).");
                throw;
            }
        }
    }
}