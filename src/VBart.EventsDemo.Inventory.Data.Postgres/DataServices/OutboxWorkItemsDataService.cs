using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Humanizer;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Persistence;

namespace VBart.EventsDemo.Inventory.Data.Postgres.DataServices
{
    public class OutboxWorkItemsDataService<TAggregate> : IWorkItemDataService<OutboxWorkItem<Change>>
    {
        private readonly JsonFormatter _formatter;
        private readonly JsonParser _parser;
        private readonly string _aggregateTypeName;
        private readonly IDbAdapter _adapter;

        public OutboxWorkItemsDataService(IDbAdapter adapter, TypeRegistry typeRegistry)
        {
            _adapter = adapter;
            _formatter = new JsonFormatter(JsonFormatter.Settings.Default.WithTypeRegistry(typeRegistry));
            _parser = new JsonParser(JsonParser.Settings.Default.WithTypeRegistry(typeRegistry));
            _aggregateTypeName = typeof(TAggregate).Name.Underscore().ToLower();
        }

        public async Task UpsertWorkItems(List<OutboxWorkItem<Change>> workItems)
        {
            if (workItems == null)
            {
                throw new ArgumentNullException(nameof(workItems));
            }

            if (workItems.Count == 0)
            {
                return;
            }

            var builder = new StringBuilder();
            var paramsDictionary = new Dictionary<string, object?>();

            for (var index = 0; index < workItems.Count; index++)
            {
                var workItem = workItems[index];
                builder
                    .AppendLine(@$"INSERT INTO public.{_aggregateTypeName}_outbox_work_item 
                                            (work_item_uuid, aggregate_uuid, payload, outbox_unix_utc_timestamp,
                                             processed_at_unix_utc_timestamp, retry_counter, next_retry_unix_utc_timestamp)
                                 VALUES")
                    .AppendLine($"(@{nameof(workItem.Uuid)}{index}, @{nameof(workItem.AggregateUuid)}{index}, @{nameof(workItem.Payload)}{index}::jsonb,")
                    .AppendLine($"@{nameof(workItem.OutboxUnixUtcTimestamp)}{index}, @{nameof(workItem.ProcessedAtUnixUtcTimestamp)}{index},")
                    .AppendLine($" @{nameof(workItem.RetryCounter)}{index}, @{nameof(workItem.NextRetryUnixUtcTimestamp)}{index})")
                    .AppendLine("ON CONFLICT (work_item_uuid) DO UPDATE SET")
                    .AppendLine($"payload = @{nameof(workItem.Payload)}{index}::jsonb,")
                    .AppendLine($"aggregate_uuid = @{nameof(workItem.AggregateUuid)}{index},")
                    .AppendLine($"outbox_unix_utc_timestamp = @{nameof(workItem.OutboxUnixUtcTimestamp)}{index}, processed_at_unix_utc_timestamp = @{nameof(workItem.ProcessedAtUnixUtcTimestamp)}{index},")
                    .AppendLine($"retry_counter = @{nameof(workItem.RetryCounter)}{index}, next_retry_unix_utc_timestamp = @{nameof(workItem.NextRetryUnixUtcTimestamp)}{index};");

                paramsDictionary.Add($"@{nameof(workItem.Uuid)}{index}", workItem.Uuid);
                paramsDictionary.Add($"@{nameof(workItem.Payload)}{index}", _formatter.Format(workItem.Payload));
                paramsDictionary.Add($"@{nameof(workItem.AggregateUuid)}{index}", workItem.AggregateUuid);
                paramsDictionary.Add($"@{nameof(workItem.OutboxUnixUtcTimestamp)}{index}", workItem.OutboxUnixUtcTimestamp);
                paramsDictionary.Add($"@{nameof(workItem.ProcessedAtUnixUtcTimestamp)}{index}", workItem.ProcessedAtUnixUtcTimestamp);
                paramsDictionary.Add($"@{nameof(workItem.RetryCounter)}{index}", workItem.RetryCounter);
                paramsDictionary.Add($"@{nameof(workItem.NextRetryUnixUtcTimestamp)}{index}", workItem.NextRetryUnixUtcTimestamp);
            }

            await _adapter.ExecuteCommand(builder.ToString(), paramsDictionary);
        }

        public async Task<List<OutboxWorkItem<Change>>> PickABatchWithDistroLock(int workItemsBatchSize, int scaleFactor)
        {
            if (workItemsBatchSize <= 0)
            {
                throw new ArgumentException("Batch size should be greater than zero.", nameof(workItemsBatchSize));
            }

            if (scaleFactor <= 0)
            {
                throw new ArgumentException("Scale factor should be greater than zero.", nameof(scaleFactor));
            }

            // ORDER BY in the query below seems redundant if you look at the function, however Dapper does some
            // default ordering unless you provide ORDER BY in the query. It does not matter to much for actual implementation
            // of the service, but contributes to predictability of the data service.
            var query = $@"SELECT 
                            work_item_uuid as {nameof(OutboxWorkItem<Change>.Uuid)},
                            payload::text as {nameof(OutboxWorkItem<Change>.Payload)},
                            aggregate_uuid as {nameof(OutboxWorkItem<Change>.AggregateUuid)},
                            outbox_unix_utc_timestamp as {nameof(OutboxWorkItem<Change>.OutboxUnixUtcTimestamp)},
                            retry_counter as {nameof(OutboxWorkItem<Change>.RetryCounter)},
                            next_retry_unix_utc_timestamp as {nameof(OutboxWorkItem<Change>.NextRetryUnixUtcTimestamp)},
                            processed_at_unix_utc_timestamp as {nameof(OutboxWorkItem<Change>.ProcessedAtUnixUtcTimestamp)}
                         FROM {_aggregateTypeName}_pick_outbox_batch({workItemsBatchSize}, {scaleFactor})
                         ORDER BY outbox_unix_utc_timestamp, aggregate_uuid;";
            return (await _adapter.GetMany<OutboxWorkItemRecord>(query)).Select(
                dynamicValue => new OutboxWorkItem<Change>()
                {
                    Uuid = dynamicValue.Uuid,
                    Payload = _parser.Parse<Change>(dynamicValue.Payload),
                    AggregateUuid = dynamicValue.AggregateUuid,
                    RetryCounter = dynamicValue.RetryCounter,
                    NextRetryUnixUtcTimestamp = dynamicValue.NextRetryUnixUtcTimestamp,
                    OutboxUnixUtcTimestamp = dynamicValue.OutboxUnixUtcTimestamp,
                    ProcessedAtUnixUtcTimestamp = dynamicValue.ProcessedAtUnixUtcTimestamp
                }).ToList();
        }

        public async Task<int> DeleteProcessedWorkItems(DateTimeOffset processedBefore)
        {
            const string deleteProcessedQuery = "DELETE FROM public.outbox_work_item WHERE processed_at_unix_utc_timestamp >= @UnixBeforeTimestamp";

            return await _adapter.ExecuteCommand(deleteProcessedQuery, new { UnixBeforeTimestamp = processedBefore.ToUnixTimeMilliseconds() });
        }

        public async Task<int> GetUnprocessedCount()
        {
            var getUnprocessedCount = $@"Select count(work_item_uuid) FROM public.{_aggregateTypeName}_outbox_work_item
                                                    WHERE processed_at_unix_utc_timestamp IS NULL";
            return await _adapter.GetSingle<int>(getUnprocessedCount);
        }

        private record OutboxWorkItemRecord
        {
            public Guid Uuid { get; init; }
            public Guid AggregateUuid { get; init; }

            public long? OutboxUnixUtcTimestamp { get; init; }

            public string? Payload { get; init; }

            public long? ProcessedAtUnixUtcTimestamp { get; init; }
            public int RetryCounter { get; init; }
            public long? NextRetryUnixUtcTimestamp { get; init; }
        }
    }
}