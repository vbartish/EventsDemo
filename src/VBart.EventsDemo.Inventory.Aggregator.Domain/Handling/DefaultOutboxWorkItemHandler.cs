using System;
using System.Threading.Tasks;
using Google.Protobuf;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Publishing;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Handling;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Handling
{
    public class DefaultOutboxWorkItemHandler<TPayload> : IWorkItemHandler<OutboxWorkItem<TPayload>>
        where TPayload : IMessage
    {
        private readonly IPublisher<TPayload> _publisher;

        public DefaultOutboxWorkItemHandler(IPublisher<TPayload> publisher)
        {
            _publisher = publisher;
        }

        public async Task Handle(OutboxWorkItem<TPayload> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            await _publisher.Publish(
                workItem.Payload ?? throw new ArgumentException("Outgoing work item did not have the payload.", nameof(workItem)),
                workItem.AggregateUuid);
        }
    }
}