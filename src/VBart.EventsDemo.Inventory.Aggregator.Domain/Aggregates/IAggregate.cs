namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates
{
    public interface IAggregate<TMetadata>
    {
        string AggregateUuid { get; set; }

        TMetadata Metadata { get; set; }

        uint InternalRevision { get; set; }

        uint LastPublishedRevision { get; set; }

        long ChangedAtUnixUtcTimestamp { get; set; }

        TLastProjectedMetadata? GetLastProjectedMetadata<TProjectedModel, TLastProjectedMetadata>(string suffix = "")
            where TLastProjectedMetadata : class;

        void SetLastProjectedMetadata<TProjectedModel, TLastProjectedMetadata>(TLastProjectedMetadata value, string suffix = "");

        bool IsCohesive();
    }
}