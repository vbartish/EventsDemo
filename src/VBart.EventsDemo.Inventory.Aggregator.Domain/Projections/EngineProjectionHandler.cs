using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using VBart.EventsDemo.Grpc.ValueObjects;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers;
using VBart.EventsDemo.Utils;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Persistence;
using Vehicle = VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates.Vehicle;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Projections
{
    public class EngineProjectionHandler : ProjectionHandlerBase<Vehicle, MetaData>
    {
        private const string _metadataSuffix = "engines";
        private readonly IAggregateSnapshotDataService<Vehicle, MetaData> _aggregateSnapshotDataService;
        private readonly ISystemClock _systemClock;

        public EngineProjectionHandler(
            IAggregateDataService<Vehicle, MetaData> aggregateDataService,
            IAggregateSnapshotDataService<Vehicle, MetaData> aggregateSnapshotDataService,
            IWorkItemGenerator<(Change PersistedEvent, Guid AggregateUuid), OutboxWorkItem<Change>> outboxWorkItemGenerator,
            IWorkItemDataService<OutboxWorkItem<Change>> outboxWorkItemsDataService,
            ISystemClock systemClock,
            ILogger<EngineProjectionHandler> logger) : base(aggregateDataService, outboxWorkItemGenerator, outboxWorkItemsDataService, logger)
        {
            _aggregateSnapshotDataService = aggregateSnapshotDataService;
            _systemClock = systemClock;
        }

        protected override async Task HandleCreate(UnwrappedDebeziumPayload unwrapped, Vehicle? aggregate)
        {
            Logger.LogDebug("Processing engine created event.");
            Vehicle suggestedState;
            if (aggregate != null)
            {
                if (aggregate.Engine != null)
                {
                    throw new InvalidOperationException(":scream: events out of order!!");
                }

                Logger.LogDebug("Vehicle aggregate was already created. Updating.");
                suggestedState = new Vehicle(aggregate);
            }
            else
            {
                suggestedState = new Vehicle();
            }

            var cdcPayload = unwrapped.GetParsedAfterPayload<DataModels.Engine>();
            suggestedState.VehicleUuid = cdcPayload.VehicleUuid.ToString();
            suggestedState.Engine = new Engine
            {
                EngineUuid = cdcPayload.EngineUuid.ToString(),
                Manufacturer = cdcPayload.Manufacturer,
                VehicleUuid = cdcPayload.VehicleUuid.ToString(),
                MaximumEngineSpeed = cdcPayload.MaximumEngineSpeed,
                MaximumMileageResource = cdcPayload.MaximumMileageResource,
                RemainingMileageResource = cdcPayload.RemainingMileageResource,
                YearOfManufacture = cdcPayload.YearOfManufacture
            };

            suggestedState.SetLastProjectedMetadata<DataModels.Engine, LsnAggregate>(unwrapped.LsnAggregate, _metadataSuffix);
            suggestedState.ChangedAtUnixUtcTimestamp = _systemClock.UtcNow.ToUnixTimeMilliseconds();
            suggestedState.InternalRevision++;

            if (suggestedState.IsCohesive())
            {
                suggestedState.LastPublishedRevision++;

                Logger.LogDebug("Vehicle aggregate is cohesive after projecting engine created event, publishing.");

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
                Logger.LogDebug("Vehicle aggregate is not cohesive after projecting engine created event, will skip publishing.");
            }

            await AggregateDataService.Upsert(suggestedState);
            Logger.LogDebug("Projecting engine created event done.");
        }

        protected override async Task HandleUpdate(UnwrappedDebeziumPayload unwrapped, Vehicle? aggregate)
        {
            Logger.LogDebug("Processing engine updated event.");

            var deserializedAfterPayload = unwrapped.GetParsedAfterPayload<DataModels.Engine>();
            var deserializedBeforePayload = unwrapped.GetParsedBeforePayload<DataModels.Engine>();
            switch (aggregate)
            {
                case null when deserializedBeforePayload.VehicleUuid == deserializedAfterPayload.VehicleUuid:
                    throw new InvalidOperationException(":scream: events out of order!!");
                case null:
                    await HandleUpdateForMissingAggregate(deserializedAfterPayload, unwrapped.LsnAggregate);
                    break;
                case not null when deserializedBeforePayload.VehicleUuid == deserializedAfterPayload.VehicleUuid:
                    await HandleRegularUpdate(deserializedAfterPayload, new(aggregate), unwrapped.LsnAggregate);
                    break;
                case not null:
                    await HandleDeletingUpdate(new(aggregate), unwrapped.LsnAggregate);
                    break;
            }


            Logger.LogDebug("Projecting engine updated event done.");
        }

        protected override async Task HandleDelete(UnwrappedDebeziumPayload unwrapped,
            Vehicle? aggregate)
        {
            if (aggregate?.Engine == null)
            {
                throw new InvalidOperationException(":scream: Events out of order!");
            }

            var suggestedState = new Vehicle(aggregate) { Engine = null };

            suggestedState.SetLastProjectedMetadata<DataModels.Engine, LsnAggregate>(unwrapped.LsnAggregate);
            suggestedState.ChangedAtUnixUtcTimestamp = _systemClock.UtcNow.ToUnixTimeMilliseconds();
            suggestedState.InternalRevision++;

            if (suggestedState.IsCohesive())
            {
                suggestedState.LastPublishedRevision++;

                Logger.LogDebug("Vehicle aggregate is cohesive after projecting engine updated event, publishing.");

                var @event = new VehicleUpdated
                {
                    Vehicle = suggestedState,
                    FieldMask = FieldMasks.FromChanges(suggestedState, suggestedState)
                };
                var changeLog = GetChangeEvent(@event, suggestedState);

                var workItems = OutboxWorkItemGenerator.GenerateFromSource(
                    (PersistedEvent: changeLog, AggregateUuid: Guid.Parse(suggestedState.AggregateUuid)));
                await OutboxWorkItemsDataService.UpsertWorkItems(workItems);
                await _aggregateSnapshotDataService.Upsert(suggestedState);
            }
            else
            {
                Logger.LogDebug("Vehicle aggregate is not cohesive after projecting engine updated event, will skip publishing.");
            }

            await AggregateDataService.Upsert(suggestedState);
        }

        protected override bool IsAlreadyProcessed(UnwrappedDebeziumPayload unwrapped,
            Vehicle? aggregate,
            Guid eventUuid)
        {
            if (aggregate == null)
            {
                return false;
            }

            var lastProcessedLsnAggregate = aggregate.GetLastProjectedMetadata<Vehicle, LsnAggregate>(_metadataSuffix);
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

        private async Task HandleDeletingUpdate(Vehicle suggestedState,
            LsnAggregate unwrappedLsnAggregate)
        {
            if (suggestedState.Engine == null)
            {
                throw new InvalidOperationException(":scream: Events out of order!");
            }

            suggestedState.Engine = null;

            suggestedState.SetLastProjectedMetadata<DataModels.Engine, LsnAggregate>(unwrappedLsnAggregate, _metadataSuffix);
            suggestedState.ChangedAtUnixUtcTimestamp = _systemClock.UtcNow.ToUnixTimeMilliseconds();
            suggestedState.InternalRevision++;

            if (suggestedState.IsCohesive())
            {
                suggestedState.LastPublishedRevision++;

                Logger.LogDebug("Vehicle aggregate is cohesive after projecting engine updated event, publishing.");

                var @event = new VehicleUpdated
                {
                    Vehicle = suggestedState,
                    FieldMask = FieldMasks.FromChanges(suggestedState, suggestedState)
                };
                var changeLog = GetChangeEvent(@event, suggestedState);

                var workItems = OutboxWorkItemGenerator.GenerateFromSource(
                    (PersistedEvent: changeLog, AggregateUuid: Guid.Parse(suggestedState.AggregateUuid)));
                await OutboxWorkItemsDataService.UpsertWorkItems(workItems);
                await _aggregateSnapshotDataService.Upsert(suggestedState);
            }
            else
            {
                Logger.LogDebug("Vehicle aggregate is not cohesive after projecting engine updated event, will skip publishing.");
            }

            await AggregateDataService.Upsert(suggestedState);
        }

        private async Task HandleRegularUpdate(
            DataModels.Engine deserializedAfterPayload,
            Vehicle suggestedState,
            LsnAggregate unwrappedLsnAggregate)
        {
            if (suggestedState.Engine == null)
            {
                throw new InvalidOperationException(":scream: Events out of order!");
            }

            suggestedState.Engine = new Engine
            {
                EngineUuid = deserializedAfterPayload.EngineUuid.ToString(),
                Manufacturer = deserializedAfterPayload.Manufacturer,
                VehicleUuid = deserializedAfterPayload.VehicleUuid.ToString(),
                MaximumEngineSpeed = deserializedAfterPayload.MaximumEngineSpeed,
                MaximumMileageResource = deserializedAfterPayload.MaximumMileageResource,
                RemainingMileageResource = deserializedAfterPayload.RemainingMileageResource,
                YearOfManufacture = deserializedAfterPayload.YearOfManufacture
            };

            suggestedState.SetLastProjectedMetadata<DataModels.Engine, LsnAggregate>(unwrappedLsnAggregate, _metadataSuffix);
            suggestedState.ChangedAtUnixUtcTimestamp = _systemClock.UtcNow.ToUnixTimeMilliseconds();
            suggestedState.InternalRevision++;

            if (suggestedState.IsCohesive())
            {
                suggestedState.LastPublishedRevision++;

                Logger.LogDebug("Vehicle aggregate is cohesive after projecting engine updated event, publishing.");

                var @event = new VehicleUpdated
                {
                    Vehicle = suggestedState,
                    FieldMask = FieldMasks.FromChanges(suggestedState, suggestedState)
                };
                var changeLog = GetChangeEvent(@event, suggestedState);

                var workItems = OutboxWorkItemGenerator.GenerateFromSource(
                    (PersistedEvent: changeLog, AggregateUuid: Guid.Parse(suggestedState.AggregateUuid)));
                await OutboxWorkItemsDataService.UpsertWorkItems(workItems);
                await _aggregateSnapshotDataService.Upsert(suggestedState);
            }
            else
            {
                Logger.LogDebug("Vehicle aggregate is not cohesive after projecting engine updated event, will skip publishing.");
            }

            await AggregateDataService.Upsert(suggestedState);
        }

        private async Task HandleUpdateForMissingAggregate(DataModels.Engine deserializedAfterPayload, LsnAggregate unwrappedLsnAggregate)
        {
            var suggestedState = new Vehicle
            {
                VehicleUuid = deserializedAfterPayload.VehicleUuid.ToString(),
                Engine = new Engine
                {
                    EngineUuid = deserializedAfterPayload.EngineUuid.ToString(),
                    Manufacturer = deserializedAfterPayload.Manufacturer,
                    VehicleUuid = deserializedAfterPayload.VehicleUuid.ToString(),
                    MaximumEngineSpeed = deserializedAfterPayload.MaximumEngineSpeed,
                    MaximumMileageResource = deserializedAfterPayload.MaximumMileageResource,
                    RemainingMileageResource = deserializedAfterPayload.RemainingMileageResource,
                    YearOfManufacture = deserializedAfterPayload.YearOfManufacture
                }
            };


            suggestedState.SetLastProjectedMetadata<DataModels.Engine, LsnAggregate>(unwrappedLsnAggregate, _metadataSuffix);
            suggestedState.ChangedAtUnixUtcTimestamp = _systemClock.UtcNow.ToUnixTimeMilliseconds();
            suggestedState.InternalRevision++;

            if (suggestedState.IsCohesive())
            {
                suggestedState.LastPublishedRevision++;

                Logger.LogDebug("Vehicle aggregate is cohesive after projecting engine updated event, publishing.");

                var @event = new VehicleUpdated
                {
                    Vehicle = suggestedState,
                    FieldMask = FieldMasks.FromChanges(suggestedState, suggestedState)
                };
                var changeLog = GetChangeEvent(@event, suggestedState);

                var workItems = OutboxWorkItemGenerator.GenerateFromSource(
                    (PersistedEvent: changeLog, AggregateUuid: Guid.Parse(suggestedState.AggregateUuid)));
                await OutboxWorkItemsDataService.UpsertWorkItems(workItems);
                await _aggregateSnapshotDataService.Upsert(suggestedState);
            }
            else
            {
                Logger.LogDebug("Vehicle aggregate is not cohesive after projecting engine updated event, will skip publishing.");
            }

            await AggregateDataService.Upsert(suggestedState);
        }
    }
}