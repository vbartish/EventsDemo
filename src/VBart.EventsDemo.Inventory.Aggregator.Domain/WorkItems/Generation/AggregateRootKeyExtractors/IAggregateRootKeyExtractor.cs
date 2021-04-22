namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation.AggregateRootKeyExtractors
{
    public interface IAggregateRootKeyExtractor<TAggregateKey, in TPayload>
    {
        bool TryExtract(TPayload payload, out TAggregateKey aggregateKey);
    }
}