using System;

namespace VBart.EventsDemo.Utils.BackoffProviders
{
    public class ExponentialBackoffDelayProvider : IBackoffDelayProvider
    {
        public long GetBackoffDelayMs(int retryAttempt, long initialDelayMs, long maxDelayMs)
        {
            var maxRetryCount = Convert.ToInt32(Math.Ceiling(Math.Log2(maxDelayMs * 1.0 / initialDelayMs)));
            var factor = Math.Min(retryAttempt + 1, maxRetryCount);
            var backoffTimestampMs = Convert.ToInt64(Math.Ceiling(initialDelayMs * Math.Pow(2, factor)));
            return Math.Min(backoffTimestampMs, maxDelayMs);
        }
    }
}