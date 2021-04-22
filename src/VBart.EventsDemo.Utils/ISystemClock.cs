using System;

namespace VBart.EventsDemo.Utils
{
    public interface ISystemClock
    {
        DateTimeOffset UtcNow { get; }
    }
}