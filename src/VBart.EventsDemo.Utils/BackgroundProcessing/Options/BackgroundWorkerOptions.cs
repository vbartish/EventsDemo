namespace VBart.EventsDemo.Utils.BackgroundProcessing.Options
{
    public record BackgroundWorkerOptions
    {
        public int WorkItemsBatchSize { get; init; } = 10;

        public int NoWorkInitialDelayMs { get; init; } = 500;

        public int NoWorkMaxDelayMs { get; init; } = 5000;

        // should be at least max horizontal scale of pods * worker threads per pod
        public int ScaleFactor { get; init; } = 12;
    }
}
