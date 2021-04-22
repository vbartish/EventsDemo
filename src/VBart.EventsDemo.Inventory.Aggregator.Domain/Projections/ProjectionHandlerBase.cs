using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Persistence;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Projections
{
    public abstract class ProjectionHandlerBase<TAggregate, TMetadata> : IProjectionHandler<Guid, JObject>
        where TAggregate : IAggregate<TMetadata>
    {
        protected ProjectionHandlerBase(
            IAggregateDataService<TAggregate, TMetadata> aggregateDataService,
            IWorkItemGenerator<(Change PersistedEvent, Guid AggregateUuid), OutboxWorkItem<Change>> outboxWorkItemGenerator,
            IWorkItemDataService<OutboxWorkItem<Change>> outboxWorkItemsDataService,
            ILogger logger)
        {
            AggregateDataService = aggregateDataService;
            OutboxWorkItemGenerator = outboxWorkItemGenerator;
            OutboxWorkItemsDataService = outboxWorkItemsDataService;
            Logger = logger;
        }

        protected SqlServerLsnAggregateComparer LsnAggregateComparer { get; } = new();

        protected IAggregateDataService<TAggregate, TMetadata> AggregateDataService { get; }

        protected IWorkItemGenerator<(Change PersistedEvent, Guid AggregateUuid), OutboxWorkItem<Change>> OutboxWorkItemGenerator { get; }

        protected IWorkItemDataService<OutboxWorkItem<Change>> OutboxWorkItemsDataService { get; }

        protected ILogger Logger { get; }

        public async Task Handle(InboundEvent<Guid, JObject> inboundEvent, Guid affectedAggregateUuid)
        {
            Logger.LogDebug(
                $"Projecting inbound event with key {inboundEvent.EventKey} and offset {inboundEvent.Offset} " +
                $"onto aggregate with uuid {affectedAggregateUuid}");
            var payload = inboundEvent.EventMessage?.GetValue(
                Constants.DebeziumMessagePayloadKey,
                StringComparison.OrdinalIgnoreCase);

            var unwrapped = new UnwrappedDebeziumPayload(payload);
            var aggregate = await AggregateDataService.Find(affectedAggregateUuid);

            if (IsAlreadyProcessed(unwrapped, aggregate, inboundEvent.EventKey))
            {
                Logger.LogDebug(
                    $"Projection of the inbound event with key {inboundEvent.EventKey} and offset {inboundEvent.Offset} " +
                    $"onto aggregate with uuid {affectedAggregateUuid} will be skipped due to immutability check.");
                return;
            }

            var handlerTask = unwrapped.Operation switch
            {
                OperationType.Create => HandleCreate(unwrapped, aggregate),
                OperationType.Read => HandleCreate(unwrapped, aggregate),
                OperationType.Update => HandleUpdate(unwrapped, aggregate),
                OperationType.Delete => HandleDelete(unwrapped, aggregate),
                _ => throw new InvalidOperationException($"Unexpected operation type {unwrapped.Operation}.")
            };
            await handlerTask;
        }

        protected Change GetChangeEvent<TEvent> (TEvent @event, Vehicle state)
            where TEvent : IMessage =>
            new()
            {
                Event = Any.Pack(@event),
                ResultingState = Any.Pack(state),
                RevisionNumber = state.LastPublishedRevision,
                ChangedAt = DateTimeOffset.FromUnixTimeMilliseconds(state.ChangedAtUnixUtcTimestamp).ToTimestamp(),
            };

        protected abstract Task HandleCreate(UnwrappedDebeziumPayload unwrapped, TAggregate? aggregate);

        protected abstract Task HandleUpdate(UnwrappedDebeziumPayload unwrapped, TAggregate? aggregate);

        protected abstract Task HandleDelete(UnwrappedDebeziumPayload unwrapped, TAggregate? aggregate);

        protected abstract bool IsAlreadyProcessed(UnwrappedDebeziumPayload unwrapped, TAggregate? aggregate, Guid eventUuid);
    }
}