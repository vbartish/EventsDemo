namespace VBart.EventsDemo.Utils.BackgroundProcessing.Options
{
    public record WorkItemProcessingOptions
    {
        public int DelayedProcessingInitialDelayMs { get; init; } = 5000;

        // hours * minutes * seconds * milliseconds
        public int DelayedProcessingMaxDelayMs { get; init; } = 24 * 60 * 60 * 1000;
    }
}
