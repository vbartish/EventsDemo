using System;
using System.Threading.Tasks;
using Autofac;
using Confluent.Kafka;
using Newtonsoft.Json.Linq;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;
using VBart.EventsDemo.Utils;

namespace VBart.EventsDemo.Kafka
{
    internal class DebeziumMessageHandler<TKey> : IKafkaMessageHandler<TKey, JObject>
    {
        private readonly Pipeline<InboundEvent<TKey, JObject>> _inboundProcessingPipeline;

        public DebeziumMessageHandler(
            Pipeline<InboundEvent<TKey, JObject>> inboundProcessingPipeline)
        {
            _inboundProcessingPipeline = inboundProcessingPipeline;
        }

        public async Task Handle(ConsumeResult<TKey, JObject> consumeResult)
        {
            if (consumeResult == null)
            {
                throw new KafkaMessageHandlingException("Consume result was not defined", new ArgumentNullException(nameof(consumeResult)));
            }

            if (consumeResult.Message == null)
            {
                throw new KafkaMessageHandlingException("ConsumeResult does not have a defined message.",
                    new ArgumentException("ConsumeResult does not have a defined message.", nameof(consumeResult)));
            }

            if (consumeResult.Message.Key == null)
            {
                throw new KafkaMessageHandlingException("ConsumeResult does not have a defined key.",
                    new ArgumentException("ConsumeResult does not have a defined key.", nameof(consumeResult)));
            }

            try
            {
                var inboundEvent = consumeResult.ToInboundEvent();
                await _inboundProcessingPipeline.Do(inboundEvent, scope => scope.Resolve<IInboundEventHandler<TKey, JObject>>().Handle(inboundEvent));
            }
            catch (Exception ex)
            {
                throw new KafkaMessageHandlingException("Unexpected exception during kafka message handling.", ex);
            }
        }
    }
}