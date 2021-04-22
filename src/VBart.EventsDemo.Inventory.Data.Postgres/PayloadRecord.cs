namespace VBart.EventsDemo.Inventory.Data.Postgres
{
    internal record PayloadRecord
    {
        public string? Payload { get; init; }
        public string? MetadataPayload { get; init; }
    }
}