using System;
using Confluent.Kafka;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates;

namespace VBart.EventsDemo.Kafka
{
    internal class ChangeSerializer : ISerializer<Change>
    {
        private readonly ILogger<ChangeSerializer> _logger;

        public ChangeSerializer(ILogger<ChangeSerializer> logger)
        {
            _logger = logger;
        }

        public byte[] Serialize(Change? data, SerializationContext context)
        {
            try
            {
                return data.ToByteArray();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Message serialization error. (topic: {context.Topic}).");
                throw;
            }
        }
    }
}