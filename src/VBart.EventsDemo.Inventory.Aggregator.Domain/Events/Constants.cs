namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Events
{
    public static class Constants
    {
        public const string DebeziumReadOperationKey = "r";
        public const string DebeziumCreateOperationKey = "c";
        public const string DebeziumUpdateOperationKey = "u";
        public const string DebeziumDeleteOperationKey = "d";
        public const string DebeziumMessagePayloadKey = "payload";
        public const string DebeziumMessageBeforeValueKey = "before";
        public const string DebeziumMessageAfterValueKey = "after";
        public const string DebeziumMessageOperationKey = "op";
        public const string DebeziumMessageSourceKey = "source";
        public const string DebeziumChangeLsnKey = "change_lsn";
        public const string DebeziumCommitLsnKey = "commit_lsn";
        public const string DebeziumTimestampKey = "ts_ms";
    }
}