using System;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems
{
    public record CdcProjectionWorkItem : WorkItemBase, IAggregateAware
    {
        public CdcProjectionWorkItem(Lsn commitLsn, Lsn changeLsn)
        {
            CommitLsn = commitLsn;
            ChangeLsn = changeLsn;
        }

        public Guid EventUuid { get; init; }
        public Guid AggregateUuid { get; init; }
        public Lsn ChangeLsn { get; init; }
        public Lsn CommitLsn { get; init; }
        public long? EventUnixUtcTimestamp { get; init; }
    }
}