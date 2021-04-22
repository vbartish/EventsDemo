using System;
using Newtonsoft.Json.Linq;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Events
{
    public class UnwrappedDebeziumPayload
    {
        private readonly string? _changeLsn;
        private readonly string _commitLsn;

        public UnwrappedDebeziumPayload(JToken? payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            _changeLsn = payload
                .SelectToken($"$.{Constants.DebeziumMessageSourceKey}.{Constants.DebeziumChangeLsnKey}")?
                .Value<string?>()?
                .ToLower();
            _commitLsn = payload
                .SelectToken($"$.{Constants.DebeziumMessageSourceKey}.{Constants.DebeziumCommitLsnKey}")?
                .Value<string>()
                ?.ToLower() ?? string.Empty;
            SourceUtcUnixTimeStamp = payload
                .SelectToken($"$.{Constants.DebeziumMessageSourceKey}.{Constants.DebeziumTimestampKey}")?
                .Value<long?>();
            PublishingUtcUnixTimeStamp = payload
                .SelectToken($"$.{Constants.DebeziumTimestampKey}")?
                .Value<long?>();

            var before = payload.SelectToken($"$.{Constants.DebeziumMessageBeforeValueKey}");
            var after = payload.SelectToken($"$.{Constants.DebeziumMessageAfterValueKey}");
            var operation = payload.SelectToken($"$.{Constants.DebeziumMessageOperationKey}")?
                .Value<string>()
                ?.ToLower();
            ValidateAndAssignPayloadsForOperation(before, after, operation);
        }

        public JToken BeforePayload { get; private set; } = new JObject();

        public JToken AfterPayload { get; private set; } = new JObject();

        public OperationType Operation { get; private set; }

        public long? SourceUtcUnixTimeStamp { get; }

        public long? PublishingUtcUnixTimeStamp { get; }

        public LsnAggregate LsnAggregate =>
            new LsnAggregate(_commitLsn, string.IsNullOrWhiteSpace(_changeLsn) ? _commitLsn : _changeLsn);

        public TPayload GetParsedBeforePayload<TPayload>() => BeforePayload.ToObject<TPayload>()??
                                                               throw new InvalidOperationException(
                                                                   $"Before payload is not deserializable to the type {typeof(TPayload)}");

        public TPayload GetParsedAfterPayload<TPayload>() => AfterPayload.ToObject<TPayload>() ??
                                                             throw new InvalidOperationException(
                                                                 $"After payload is not deserializable to the type {typeof(TPayload)}");

        private void ValidateAndAssignPayloadsForOperation(
            JToken? beforePayload,
            JToken? afterPayload,
            string? operation)
        {
            switch (operation)
            {
                case Constants.DebeziumCreateOperationKey:
                    Operation = OperationType.Create;
                    if (beforePayload?.HasValues == true)
                    {
                        throw new ArgumentException($"Before payload is not applicable for {Operation} operations.", nameof(beforePayload));
                    }

                    if (afterPayload?.HasValues != true)
                    {
                        throw new ArgumentException($"No after state payload for {Operation} operation.", nameof(afterPayload));
                    }

                    AfterPayload = afterPayload!;
                    break;
                case Constants.DebeziumReadOperationKey:
                    Operation = OperationType.Read;
                    if (beforePayload?.HasValues == true)
                    {
                        throw new ArgumentException($"Before payload is not applicable for {Operation} operations.", nameof(beforePayload));
                    }

                    if (afterPayload?.HasValues != true)
                    {
                        throw new ArgumentException($"No after state payload for {Operation} operation.", nameof(afterPayload));
                    }

                    AfterPayload = afterPayload!;
                    break;
                case Constants.DebeziumUpdateOperationKey:
                    Operation = OperationType.Update;
                    if (beforePayload?.HasValues != true)
                    {
                        throw new ArgumentException($"No before state payload for {Operation} operation.", nameof(beforePayload));
                    }

                    if (afterPayload?.HasValues != true)
                    {
                        throw new ArgumentException($"No after state payload for {Operation} operation.", nameof(afterPayload));
                    }

                    BeforePayload = beforePayload!;
                    AfterPayload = afterPayload!;
                    break;
                case Constants.DebeziumDeleteOperationKey:
                    Operation = OperationType.Delete;
                    if (afterPayload?.HasValues == true)
                    {
                        throw new ArgumentException($"After payload is not applicable for {Operation} operations.", nameof(afterPayload));
                    }

                    if (beforePayload?.HasValues != true)
                    {
                        throw new ArgumentException($"No before state payload for {Operation} operation.", nameof(beforePayload));
                    }

                    BeforePayload = beforePayload!;
                    break;
                default:
                    throw new ArgumentException($"Could not identify the operation from the debezium message payload: {operation}.", nameof(operation));
            }
        }
    }
}