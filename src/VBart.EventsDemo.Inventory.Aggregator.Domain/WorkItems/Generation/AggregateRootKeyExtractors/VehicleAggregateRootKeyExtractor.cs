using System;
using VBart.EventsDemo.Inventory.DataModels;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation.AggregateRootKeyExtractors
{
    public class VehicleAggregateRootKeyExtractor: IAggregateRootKeyExtractor<Guid, Vehicle>
    {
        public bool TryExtract(Vehicle payload, out Guid aggregateKey)
        {
            aggregateKey = payload.VehicleUuid;
            return true;
        }
    }
}