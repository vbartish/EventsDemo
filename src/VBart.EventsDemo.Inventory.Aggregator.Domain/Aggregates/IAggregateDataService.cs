using System;
using System.Threading.Tasks;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates
{
    public interface IAggregateDataService<TAggregate, TMetadata>
        where TAggregate : IAggregate<TMetadata>
    {
        Task<TAggregate?> Find(Guid aggregateUuid);

        Task Upsert(TAggregate aggregate);

        Task Delete(Guid aggregateUuid);
    }
}