using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation.AggregateRootKeyExtractors;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation
{
    public class SingleAggregateCdcProjectionWorkItemGenerator<TPayload> : CdcProjectionWorkItemGeneratorBase<TPayload>
    {
        private readonly IAggregateRootKeyExtractor<Guid, TPayload> _keyExtractor;

        public SingleAggregateCdcProjectionWorkItemGenerator(
            ILogger<SingleAggregateCdcProjectionWorkItemGenerator<TPayload>> logger,
            IAggregateRootKeyExtractor<Guid, TPayload> keyExtractor)
            : base(logger)
        {
            _keyExtractor = keyExtractor;
        }

        protected override List<CdcProjectionWorkItem> GenerateWorkItemsForCreateOperation(
            Guid persistedEventUuid,
            TPayload afterPayload,
            LsnAggregate lsnAggregate,
            long? eventUnixUtcTimestamp)
        {
            if (!_keyExtractor.TryExtract(afterPayload, out var aggregateKey))
            {
                return new List<CdcProjectionWorkItem>();
            }

            return new List<CdcProjectionWorkItem>
            {
                new(lsnAggregate.CommitLsn, lsnAggregate.ChangeLsn)
                {
                    Uuid = Guid.NewGuid(),
                    AggregateUuid = aggregateKey,
                    EventUuid = persistedEventUuid,
                    EventUnixUtcTimestamp = eventUnixUtcTimestamp
                }
            };
        }

        protected override List<CdcProjectionWorkItem> GenerateWorkItemsForDeleteOperation(
            Guid persistedEventUuid,
            TPayload beforePayload,
            LsnAggregate lsnAggregate,
            long? eventUnixUtcTimestamp)
        {
            if (!_keyExtractor.TryExtract(beforePayload, out var aggregateKey))
            {
                return new List<CdcProjectionWorkItem>();
            }

            return new List<CdcProjectionWorkItem>
            {
                new(lsnAggregate.CommitLsn, lsnAggregate.ChangeLsn)
                {
                    Uuid = Guid.NewGuid(),
                    AggregateUuid = aggregateKey,
                    EventUuid = persistedEventUuid,
                    EventUnixUtcTimestamp = eventUnixUtcTimestamp
                }
            };
        }

        protected override List<CdcProjectionWorkItem> GenerateWorkItemsForUpdateOperation(
            Guid persistedEventUuid,
            TPayload beforePayload,
            TPayload afterPayload,
            LsnAggregate lsnAggregate,
            long? eventUnixUtcTimestamp)
        {
            var workItemsForAffectedAggregates = new List<CdcProjectionWorkItem>();

            if (_keyExtractor.TryExtract(beforePayload, out var beforeAggregateKey))
            {
                workItemsForAffectedAggregates.Add(new CdcProjectionWorkItem(lsnAggregate.CommitLsn, lsnAggregate.ChangeLsn)
                {
                    Uuid = Guid.NewGuid(),
                    AggregateUuid = beforeAggregateKey,
                    EventUuid = persistedEventUuid,
                    EventUnixUtcTimestamp = eventUnixUtcTimestamp
                });
            }

            if (_keyExtractor.TryExtract(afterPayload, out var afterAggregateKey)
                && beforeAggregateKey != afterAggregateKey)
            {
                workItemsForAffectedAggregates.Add(new(lsnAggregate.CommitLsn, lsnAggregate.ChangeLsn)
                {
                    Uuid = Guid.NewGuid(),
                    AggregateUuid = afterAggregateKey,
                    EventUuid = persistedEventUuid,
                    EventUnixUtcTimestamp = eventUnixUtcTimestamp
                });
            }

            return workItemsForAffectedAggregates;
        }
    }
}