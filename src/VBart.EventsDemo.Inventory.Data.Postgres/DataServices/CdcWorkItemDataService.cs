using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Humanizer;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Persistence;

namespace VBart.EventsDemo.Inventory.Data.Postgres.DataServices
{
    public class CdcWorkItemDataService<TAggregate> : IWorkItemDataService<CdcProjectionWorkItem>
    {
        private readonly string _aggregateTypeName;
        private readonly IDbAdapter _adapter;

        public CdcWorkItemDataService(IDbAdapter adapter)
        {
            _adapter = adapter;
            _aggregateTypeName = typeof(TAggregate).Name.Underscore().ToLower();
        }

        public async Task UpsertWorkItems(List<CdcProjectionWorkItem> workItems)
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
                var workItemRecord = new CdcProjectionWorkItemRecord(workItems[index]);
                builder
                    .AppendLine(@$"INSERT INTO public.{_aggregateTypeName}_cdc_work_item 
                                            (cdc_work_item_uuid, event_uuid, aggregate_uuid,
                                             change_vlf_sequence_number, change_log_block_offset, change_log_block_slot_number,
                                             commit_vlf_sequence_number, commit_log_block_offset, commit_log_block_slot_number,
                                             event_unix_utc_timestamp, processed_at_unix_utc_timestamp, retry_counter,
                                             next_retry_unix_utc_timestamp)
                                 VALUES")
                    .AppendLine($"(@{nameof(CdcProjectionWorkItemRecord.Uuid)}{index}, @{nameof(CdcProjectionWorkItem.EventUuid)}{index}, @{nameof(CdcProjectionWorkItem.AggregateUuid)}{index},")
                    .AppendLine($"@{nameof(CdcProjectionWorkItemRecord.ChangeVlfSequenceNumber)}{index}, @{nameof(CdcProjectionWorkItemRecord.ChangeLogBlockOffset)}{index}, @{nameof(CdcProjectionWorkItemRecord.ChangeLogBlockSlotNumber)}{index},")
                    .AppendLine($"@{nameof(CdcProjectionWorkItemRecord.CommitVlfSequenceNumber)}{index}, @{nameof(CdcProjectionWorkItemRecord.CommitLogBlockOffset)}{index}, @{nameof(CdcProjectionWorkItemRecord.CommitLogBlockSlotNumber)}{index},")
                    .AppendLine($"@{nameof(CdcProjectionWorkItemRecord.EventUnixUtcTimestamp)}{index}, @{nameof(CdcProjectionWorkItem.ProcessedAtUnixUtcTimestamp)}{index},")
                    .AppendLine($" @{nameof(CdcProjectionWorkItemRecord.RetryCounter)}{index}, @{nameof(CdcProjectionWorkItem.NextRetryUnixUtcTimestamp)}{index})")
                    .AppendLine("ON CONFLICT (cdc_work_item_uuid) DO UPDATE SET")
                    .AppendLine($"event_uuid = @{nameof(CdcProjectionWorkItemRecord.EventUuid)}{index}, aggregate_uuid = @{nameof(CdcProjectionWorkItemRecord.AggregateUuid)}{index},")
                    .AppendLine($"change_vlf_sequence_number = @{nameof(CdcProjectionWorkItemRecord.ChangeVlfSequenceNumber)}{index}, change_log_block_offset = @{nameof(CdcProjectionWorkItemRecord.ChangeLogBlockOffset)}{index}, change_log_block_slot_number = @{nameof(CdcProjectionWorkItemRecord.ChangeLogBlockSlotNumber)}{index},")
                    .AppendLine($"commit_vlf_sequence_number = @{nameof(CdcProjectionWorkItemRecord.CommitVlfSequenceNumber)}{index}, commit_log_block_offset = @{nameof(CdcProjectionWorkItemRecord.CommitLogBlockOffset)}{index}, commit_log_block_slot_number = @{nameof(CdcProjectionWorkItemRecord.CommitLogBlockSlotNumber)}{index},")
                    .AppendLine($"event_unix_utc_timestamp = @{nameof(CdcProjectionWorkItemRecord.EventUnixUtcTimestamp)}{index}, processed_at_unix_utc_timestamp = @{nameof(CdcProjectionWorkItemRecord.ProcessedAtUnixUtcTimestamp)}{index},")
                    .AppendLine($"retry_counter = @{nameof(CdcProjectionWorkItemRecord.RetryCounter)}{index}, next_retry_unix_utc_timestamp = @{nameof(CdcProjectionWorkItemRecord.NextRetryUnixUtcTimestamp)}{index};");

                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.Uuid)}{index}", workItemRecord.Uuid);
                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.EventUuid)}{index}", workItemRecord.EventUuid);
                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.AggregateUuid)}{index}", workItemRecord.AggregateUuid);
                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.ChangeVlfSequenceNumber)}{index}", workItemRecord.ChangeVlfSequenceNumber);
                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.ChangeLogBlockOffset)}{index}", workItemRecord.ChangeLogBlockOffset);
                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.ChangeLogBlockSlotNumber)}{index}", workItemRecord.ChangeLogBlockSlotNumber);
                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.CommitVlfSequenceNumber)}{index}", workItemRecord.CommitVlfSequenceNumber);
                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.CommitLogBlockOffset)}{index}", workItemRecord.CommitLogBlockOffset);
                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.CommitLogBlockSlotNumber)}{index}", workItemRecord.CommitLogBlockSlotNumber);
                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.EventUnixUtcTimestamp)}{index}", workItemRecord.EventUnixUtcTimestamp);
                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.ProcessedAtUnixUtcTimestamp)}{index}", workItemRecord.ProcessedAtUnixUtcTimestamp);
                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.RetryCounter)}{index}", workItemRecord.RetryCounter);
                paramsDictionary.Add($"@{nameof(CdcProjectionWorkItemRecord.NextRetryUnixUtcTimestamp)}{index}", workItemRecord.NextRetryUnixUtcTimestamp);
            }

            await _adapter.ExecuteCommand(builder.ToString(), paramsDictionary);
        }

        public async Task<List<CdcProjectionWorkItem>> PickABatchWithDistroLock(int workItemsBatchSize, int scaleFactor)
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
                            cdc_work_item_uuid as {nameof(CdcProjectionWorkItemRecord.Uuid)},
                            event_uuid as {nameof(CdcProjectionWorkItemRecord.EventUuid)},
                            aggregate_uuid as {nameof(CdcProjectionWorkItemRecord.AggregateUuid)},
                            change_vlf_sequence_number as {nameof(CdcProjectionWorkItemRecord.ChangeVlfSequenceNumber)},
                            change_log_block_offset as {nameof(CdcProjectionWorkItemRecord.ChangeLogBlockOffset)},
                            change_log_block_slot_number as {nameof(CdcProjectionWorkItemRecord.ChangeLogBlockSlotNumber)},
                            commit_vlf_sequence_number as {nameof(CdcProjectionWorkItemRecord.CommitVlfSequenceNumber)},
                            commit_log_block_offset as {nameof(CdcProjectionWorkItemRecord.CommitLogBlockOffset)},
                            commit_log_block_slot_number as {nameof(CdcProjectionWorkItemRecord.CommitLogBlockSlotNumber)},
                            event_unix_utc_timestamp as {nameof(CdcProjectionWorkItemRecord.EventUnixUtcTimestamp)},
                            retry_counter as {nameof(CdcProjectionWorkItemRecord.RetryCounter)},
                            next_retry_unix_utc_timestamp as {nameof(CdcProjectionWorkItemRecord.NextRetryUnixUtcTimestamp)},
                            processed_at_unix_utc_timestamp as {nameof(CdcProjectionWorkItemRecord.ProcessedAtUnixUtcTimestamp)}
                         FROM {_aggregateTypeName}_pick_work_items_batch({workItemsBatchSize}, {scaleFactor})
                         ORDER BY commit_vlf_sequence_number, commit_log_block_offset, commit_log_block_slot_number,
                                    change_vlf_sequence_number, change_log_block_offset, change_log_block_slot_number,
                                    aggregate_uuid;";
            return (await _adapter.GetMany<CdcProjectionWorkItemRecord>(query)).Select(record => record.ToCdcProjectionWorkItem()).ToList();
        }

        public async Task<int> DeleteProcessedWorkItems(DateTimeOffset processedBefore)
        {
            const string deleteProcessedQuery = "DELETE FROM public.cdc_work_item WHERE processed_at_unix_utc_timestamp >= @UnixBeforeTimestamp";

            return await _adapter.ExecuteCommand(deleteProcessedQuery, new { UnixBeforeTimestamp = processedBefore.ToUnixTimeMilliseconds() });
        }

        public async Task<int> GetUnprocessedCount()
        {
            var getUnprocessedCount = $@"Select count(cdc_work_item_uuid) FROM public.{_aggregateTypeName}_cdc_work_item
                                                    WHERE processed_at_unix_utc_timestamp IS NULL";
            return await _adapter.GetSingle<int>(getUnprocessedCount);
        }

        private record CdcProjectionWorkItemRecord
        {
            public CdcProjectionWorkItemRecord()
            {
            }

            public CdcProjectionWorkItemRecord(CdcProjectionWorkItem item)
            {
                Uuid = item.Uuid;
                AggregateUuid = item.AggregateUuid;
                EventUuid = item.EventUuid;
                RetryCounter = item.RetryCounter;
                NextRetryUnixUtcTimestamp = item.NextRetryUnixUtcTimestamp;
                ProcessedAtUnixUtcTimestamp = item.ProcessedAtUnixUtcTimestamp;
                EventUnixUtcTimestamp = item.EventUnixUtcTimestamp;
                CommitVlfSequenceNumber = item.CommitLsn.VlfSequenceNumber;
                CommitLogBlockOffset = item.CommitLsn.LogBlockOffset;
                CommitLogBlockSlotNumber = item.CommitLsn.LogBlockSlotNumber;
                ChangeVlfSequenceNumber = item.ChangeLsn.VlfSequenceNumber;
                ChangeLogBlockOffset = item.ChangeLsn.LogBlockOffset;
                ChangeLogBlockSlotNumber = item.ChangeLsn.LogBlockSlotNumber;
            }

            public Guid Uuid { get; init; }
            public Guid EventUuid { get; init; }
            public Guid AggregateUuid { get; init; }
            public long CommitVlfSequenceNumber { get; init; }
            public long CommitLogBlockOffset { get; init; }
            public long CommitLogBlockSlotNumber { get; init; }
            public long ChangeVlfSequenceNumber { get; init; }
            public long ChangeLogBlockOffset { get; init; }
            public long ChangeLogBlockSlotNumber { get; init; }
            public long? EventUnixUtcTimestamp { get; init; }
            public long? ProcessedAtUnixUtcTimestamp { get; init; }
            public int RetryCounter { get; init; }
            public long? NextRetryUnixUtcTimestamp { get; init; }

            public CdcProjectionWorkItem ToCdcProjectionWorkItem()
            {
                var commitLsn = new Lsn(CommitVlfSequenceNumber, CommitLogBlockOffset, CommitLogBlockSlotNumber);
                var changeLsn = new Lsn(ChangeVlfSequenceNumber, ChangeLogBlockOffset, ChangeLogBlockSlotNumber);
                return new(commitLsn, changeLsn)
                {
                    Uuid = Uuid,
                    AggregateUuid = AggregateUuid,
                    EventUuid = EventUuid,
                    RetryCounter = RetryCounter,
                    EventUnixUtcTimestamp = EventUnixUtcTimestamp,
                    NextRetryUnixUtcTimestamp = NextRetryUnixUtcTimestamp,
                    ProcessedAtUnixUtcTimestamp = ProcessedAtUnixUtcTimestamp
                };
            }
        }
    }
}