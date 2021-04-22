using System.Collections.Immutable;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Events
{
    public class InboundEvent<TKey, TMessage>
    {
        public TKey? EventKey { get; init; }

        public TMessage? EventMessage { get; init; }

        public string Source { get; init; } = string.Empty;

        public long Offset { get; init; } = long.MinValue;

        public ImmutableDictionary<string, byte[]> Metadata { get; init; } = ImmutableDictionary<string, byte[]>.Empty;

        public long UnixUtcTimestampMs { get; set; } = 0;
    }
}