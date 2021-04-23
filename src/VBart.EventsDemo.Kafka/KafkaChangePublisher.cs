using System;
using System.Threading.Tasks;
using Confluent.Kafka;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Publishing;

namespace VBart.EventsDemo.Kafka
{
    public class KafkaChangePublisher : IPublisher<Change>
    {
        private readonly IProducer<string, Change> _producer;

        public KafkaChangePublisher(IProducer<string, Change> producer)
        {
            _producer = producer;
        }

        public async Task Publish(Change payload, Guid aggregateUuid)
        {
            var topicName = GetTopicName(payload);
            var message = new Message<string, Change>
            {
                Key = aggregateUuid.ToString(),
                Value = payload
            };
            await _producer.ProduceAsync(topicName, message);
        }

        private static string GetTopicName(Change payload)
        {
            if (payload.Event.Is(VehicleCreated.Descriptor)
                || payload.Event.Is(VehicleUpdated.Descriptor)
                || payload.Event.Is(VehicleDeleted.Descriptor))
            {
                return "Vehicles";
            }

            throw new ArgumentException("Could not map the outbox payload to a topic.", nameof(payload));
        }
    }
}