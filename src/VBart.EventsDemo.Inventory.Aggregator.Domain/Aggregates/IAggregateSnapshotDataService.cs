using System;
using System.Threading.Tasks;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates
{
    public interface IAggregateSnapshotDataService<TAggregate, TMetadata>
    where TAggregate : IAggregate<TMetadata>
    {
        Task<TAggregate?> Find(Guid aggregateUuid);

        Task Upsert(TAggregate snapshot);

        Task Delete(Guid aggregateUuid);
    }
}