using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation
{
    public abstract class CdcProjectionWorkItemGeneratorBase<TPayload>
        : IWorkItemGenerator<(InboundEvent<Guid, JObject> PersistedEvent, Guid PersistedEventUuid), CdcProjectionWorkItem>
    {
        protected CdcProjectionWorkItemGeneratorBase(ILogger<CdcProjectionWorkItemGeneratorBase<TPayload>> logger)
        {
            Logger = logger;
        }

        protected ILogger<CdcProjectionWorkItemGeneratorBase<TPayload>> Logger { get; }

        public List<CdcProjectionWorkItem> GenerateFromSource((InboundEvent<Guid, JObject> PersistedEvent, Guid PersistedEventUuid) source)
        {
            var payload = source.PersistedEvent.EventMessage?.GetValue(
                Constants.DebeziumMessagePayloadKey,
                StringComparison.OrdinalIgnoreCase);
            if (payload?.HasValues != true)
            {
                Logger.LogInformation($"Ignoring empty payload for inbound event {source.PersistedEventUuid}.");
                return new List<CdcProjectionWorkItem>();
            }

            var unwrapped = new UnwrappedDebeziumPayload(payload);

            return unwrapped.Operation switch
            {
                OperationType.Create => GenerateWorkItemsForCreateOperation(
                    source.PersistedEventUuid,
                    unwrapped.GetParsedAfterPayload<TPayload>(),
                    unwrapped.LsnAggregate,
                    unwrapped.SourceUtcUnixTimeStamp),
                OperationType.Read => GenerateWorkItemsForCreateOperation(
                    source.PersistedEventUuid,
                    unwrapped.GetParsedAfterPayload<TPayload>(),
                    unwrapped.LsnAggregate,
                    unwrapped.SourceUtcUnixTimeStamp),
                OperationType.Update => GenerateWorkItemsForUpdateOperation(
                    source.PersistedEventUuid,
                    unwrapped.GetParsedBeforePayload<TPayload>(),
                    unwrapped.GetParsedAfterPayload<TPayload>(),
                    unwrapped.LsnAggregate,
                    unwrapped.SourceUtcUnixTimeStamp),
                OperationType.Delete => GenerateWorkItemsForDeleteOperation(
                    source.PersistedEventUuid,
                    unwrapped.GetParsedBeforePayload<TPayload>(),
                    unwrapped.LsnAggregate,
                    unwrapped.SourceUtcUnixTimeStamp),
                _ => throw new InvalidOperationException($"Unexpected debezium operation type {unwrapped.Operation}.")
            };
        }

        protected abstract List<CdcProjectionWorkItem> GenerateWorkItemsForCreateOperation(
            Guid persistedEventUuid,
            TPayload afterPayload,
            LsnAggregate lsnAggregate,
            long? eventUnixUtcTimestamp);

        protected abstract List<CdcProjectionWorkItem> GenerateWorkItemsForUpdateOperation(
            Guid persistedEventUuid,
            TPayload beforePayload,
            TPayload afterPayload,
            LsnAggregate lsnAggregate,
            long? eventUnixUtcTimestamp);

        protected abstract List<CdcProjectionWorkItem> GenerateWorkItemsForDeleteOperation(
            Guid persistedEventUuid,
            TPayload beforePayload,
            LsnAggregate lsnAggregate,
            long? eventUnixUtcTimestamp);
    }
}