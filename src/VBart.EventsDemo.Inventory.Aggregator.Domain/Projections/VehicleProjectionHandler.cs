using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers;
using VBart.EventsDemo.Utils;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Persistence;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Projections
{
    public class VehicleProjectionHandler : ProjectionHandlerBase<Vehicle, MetaData>
    {
        private const string MetadataSuffix = "vehicle";
        private readonly IAggregateSnapshotDataService<Vehicle, MetaData> _aggregateSnapshotDataService;
        private readonly ISystemClock _systemClock;

        public VehicleProjectionHandler(
            IAggregateDataService<Vehicle, MetaData> aggregateDataService,
            IAggregateSnapshotDataService<Vehicle, MetaData> aggregateSnapshotDataService,
            IWorkItemGenerator<(Change PersistedEvent, Guid AggregateUuid), OutboxWorkItem<Change>> outboxWorkItemGenerator,
            IWorkItemDataService<OutboxWorkItem<Change>> outboxWorkItemsDataService,
            ISystemClock systemClock,
            ILogger<VehicleProjectionHandler> logger)
            : base(aggregateDataService, outboxWorkItemGenerator, outboxWorkItemsDataService, logger)
        {
            _aggregateSnapshotDataService = aggregateSnapshotDataService;
            _systemClock = systemClock;
        }

        protected override async Task HandleCreate(UnwrappedDebeziumPayload unwrapped, Vehicle? aggregate)
        {
            Logger.LogDebug("Processing vehicle created event.");
            Vehicle suggestedState;
            if (aggregate != null)
            {
                Logger.LogDebug("Vehicle aggregate was already created. Updating.");
                suggestedState = new Vehicle(aggregate);
            }
            else
            {
                suggestedState = new Vehicle();
            }

            var cdcPayload = unwrapped.GetParsedAfterPayload<DataModels.Vehicle>();
            suggestedState.VehicleUuid = cdcPayload.VehicleUuid.ToString();
            suggestedState.Model = cdcPayload.Model;
            suggestedState.YearOfManufacture = cdcPayload.YearOfManufacture;

            suggestedState.SetLastProjectedMetadata<DataModels.Vehicle, LsnAggregate>(unwrapped.LsnAggregate, MetadataSuffix);
            suggestedState.ChangedAtUnixUtcTimestamp = _systemClock.UtcNow.ToUnixTimeMilliseconds();
            suggestedState.InternalRevision++;

            if (suggestedState.IsCohesive())
            {
                suggestedState.LastPublishedRevision++;

                Logger.LogDebug("Vehicle aggregate is cohesive after projecting created event, publishing.");

                var @event = aggregate == null
                    ? new VehicleCreated { Vehicle = suggestedState }
                    : (IMessage)new VehicleUpdated
                    {
                        Vehicle = suggestedState,
                        FieldMask = FieldMasks.FromChanges(aggregate, suggestedState)
                    };
                var changeLog = GetChangeEvent(@event, suggestedState);

                var workItems = OutboxWorkItemGenerator.GenerateFromSource(
                    (PersistedEvent: changeLog, AggregateUuid: Guid.Parse(suggestedState.AggregateUuid)));
                await OutboxWorkItemsDataService.UpsertWorkItems(workItems);
                await _aggregateSnapshotDataService.Upsert(suggestedState);
            }
            else
            {
                Logger.LogDebug("Vehicle aggregate is not cohesive after projecting created event, will skip publishing.");
            }

            await AggregateDataService.Upsert(suggestedState);
            Logger.LogDebug("Vehicle aggregate created.");
        }

        protected override async Task HandleUpdate(UnwrappedDebeziumPayload unwrapped,
            Vehicle? aggregate)
        {
            Logger.LogDebug("Processing vehicle updated event.");
            if (aggregate == null)
            {
                throw new InvalidOperationException(":scream: events out of order!!");
            }

            var suggestedState = new Vehicle(aggregate);

            var cdcPayload = unwrapped.GetParsedAfterPayload<DataModels.Vehicle>();
            suggestedState.Model = cdcPayload.Model;
            suggestedState.YearOfManufacture = cdcPayload.YearOfManufacture;

            suggestedState.SetLastProjectedMetadata<DataModels.Vehicle, LsnAggregate>(unwrapped.LsnAggregate, MetadataSuffix);
            suggestedState.ChangedAtUnixUtcTimestamp = _systemClock.UtcNow.ToUnixTimeMilliseconds();
            suggestedState.InternalRevision++;

            if (suggestedState.IsCohesive())
            {
                suggestedState.LastPublishedRevision++;

                Logger.LogDebug("Vehicle aggregate is cohesive after projecting updated event, publishing.");

                var @event = new VehicleUpdated
                {
                    Vehicle = suggestedState,
                    FieldMask = FieldMasks.FromChanges(aggregate, suggestedState)
                };
                var changeLog = GetChangeEvent(@event, suggestedState);

                var workItems = OutboxWorkItemGenerator.GenerateFromSource(
                    (PersistedEvent: changeLog, AggregateUuid: Guid.Parse(suggestedState.AggregateUuid)));
                await OutboxWorkItemsDataService.UpsertWorkItems(workItems);
                await _aggregateSnapshotDataService.Upsert(suggestedState);
            }
            else
            {
                Logger.LogDebug("Vehicle aggregate is not cohesive after projecting updated event, will skip publishing.");
            }

            await AggregateDataService.Upsert(suggestedState);
            Logger.LogDebug("Processing Vehicle created event done.");
        }

        protected override async Task HandleDelete(UnwrappedDebeziumPayload unwrapped, Vehicle? aggregate)
        {
            if (aggregate == null)
            {
                throw new InvalidOperationException(":scream: events out of order!!");
            }

            if (aggregate.Engine != null)
            {
                throw new InvalidOperationException(
                    "Can't process just yet, dependent entity has to be deleted first.");
            }

            var aggregateUuid = Guid.Parse(aggregate.VehicleUuid);

            await AggregateDataService.Delete(aggregateUuid);
            var publishedSnapshot = await _aggregateSnapshotDataService.Find(aggregateUuid);
            if (publishedSnapshot == null)
            {
                Logger.LogDebug("Vehicle aggregate never got published. Will skip publishing of the tombstone.");
                return;
            }

            Logger.LogDebug($"Vehicle aggregate was published earlier. Will publish deleted event.");

            var tombstoneEvent = new Change
            {
                Event = Any.Pack(new VehicleDeleted
                {
                    VehicleUuid = aggregate.VehicleUuid,
                    VehicleRevisionNumber = aggregate.LastPublishedRevision
                }),
                ResultingState = Any.Pack(new Empty()),
                RevisionNumber = aggregate.LastPublishedRevision + 1,
                ChangedAt = _systemClock.UtcNow.ToTimestamp(),
            };

            await _aggregateSnapshotDataService.Delete(aggregateUuid);
            var workItems = OutboxWorkItemGenerator.GenerateFromSource(
                (PersistedEvent: tombstoneEvent, AggregateUuid: aggregateUuid));
            await OutboxWorkItemsDataService.UpsertWorkItems(workItems);
        }

        protected override bool IsAlreadyProcessed(UnwrappedDebeziumPayload unwrapped,
            Vehicle? aggregate,
            Guid eventUuid)
        {
                if (aggregate == null)
                {
                    return false;
                }

                var lastProcessedLsnAggregate = aggregate.GetLastProjectedMetadata<Vehicle, LsnAggregate>(MetadataSuffix);
                if (lastProcessedLsnAggregate == null)
                {
                    Logger.LogWarning("Was not able to get last projected LSNs from aggregate metadata. Reprocessing.");
                    return false;
                }

                var wasProcessed =
                    LsnAggregateComparer.Compare(lastProcessedLsnAggregate, unwrapped.LsnAggregate) >= 0;

                Logger.LogDebug($"Idempotency check {(wasProcessed ? $"successful, skipping event {eventUuid}." : $"not successful, projecting event {eventUuid}.")}");
                return wasProcessed;
        }
    }
}