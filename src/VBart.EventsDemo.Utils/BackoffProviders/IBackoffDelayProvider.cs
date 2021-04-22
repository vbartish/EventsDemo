namespace VBart.EventsDemo.Utils.BackoffProviders
{
    public interface IBackoffDelayProvider
    {
        long GetBackoffDelayMs(int retryAttempt, long initialDelayMs, long maxDelayMs);
    }
}