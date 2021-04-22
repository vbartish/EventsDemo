using System;
using VBart.EventsDemo.Inventory.DataModels;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation.AggregateRootKeyExtractors
{
    public class EngineAggregateRootKeyExtractor : IAggregateRootKeyExtractor<Guid, Engine>
    {
        public bool TryExtract(Engine payload, out Guid aggregateKey)
        {
            if (payload.VehicleUuid != null)
            {
                aggregateKey = payload.VehicleUuid.Value;
                return true;
            }

            aggregateKey = Guid.Empty;
            return false;
        }
    }
}