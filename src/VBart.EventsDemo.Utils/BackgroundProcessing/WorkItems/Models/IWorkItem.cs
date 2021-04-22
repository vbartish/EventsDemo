using System;

namespace VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models
{
    public interface IWorkItem
    {
        Guid Uuid { get; init; }

        long? ProcessedAtUnixUtcTimestamp { get; init; }

        int RetryCounter { get; init; }

        long? NextRetryUnixUtcTimestamp { get; init; }
    }
}
