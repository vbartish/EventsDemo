using System;
using Google.Protobuf;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems
{
    public record OutboxWorkItem<TPayload> : WorkItemBase, IAggregateAware
        where TPayload : IMessage
    {
        public Guid AggregateUuid { get; init; }
        public long? OutboxUnixUtcTimestamp { get; init; }
        public TPayload? Payload { get; init; }
    }
}