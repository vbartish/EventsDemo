using System;

namespace VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models
{
    public record WorkItemBase : IWorkItem
    {
        public Guid Uuid { get; init; }
        public long? ProcessedAtUnixUtcTimestamp { get; init; }
        public int RetryCounter { get; init; }
        public long? NextRetryUnixUtcTimestamp { get; init; }
    }
}
