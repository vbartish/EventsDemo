using System;
using System.Collections.Generic;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation
{
    public class OutboxWorkItemGenerator : IWorkItemGenerator<(Change PersistedEvent, Guid AggregateUuid), OutboxWorkItem<Change>>
    {
        public List<OutboxWorkItem<Change>> GenerateFromSource(
            (Change PersistedEvent, Guid AggregateUuid) source) =>
            new()
            {
                new OutboxWorkItem<Change>
                {
                    Uuid = Guid.NewGuid(),
                    Payload = source.PersistedEvent,
                    AggregateUuid = source.AggregateUuid,
                    OutboxUnixUtcTimestamp = source.PersistedEvent.ChangedAt.ToDateTimeOffset().ToUnixTimeMilliseconds()
                }
            };
    }
}