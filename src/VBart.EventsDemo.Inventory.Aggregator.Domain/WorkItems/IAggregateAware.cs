using System;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems
{
    public interface IAggregateAware
    {
        Guid AggregateUuid { get; }
    }
}