using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Confluent.Kafka;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;

namespace VBart.EventsDemo.Kafka
{
    internal static class ConsumeResultExtensions
    {
        public static InboundEvent<TKey, TMessage> ToInboundEvent<TKey, TMessage>(
            this ConsumeResult<TKey, TMessage> consumeResult)
        {
            if (consumeResult == null)
            {
                throw new ArgumentNullException(nameof(consumeResult));
            }

            return new InboundEvent<TKey, TMessage>
            {
                Source = consumeResult.Topic,
                Offset = consumeResult.Offset.Value,
                EventKey = consumeResult.Message.Key,
                EventMessage = consumeResult.Message.Value,
                UnixUtcTimestampMs = consumeResult.Message.Timestamp.UnixTimestampMs,
                Metadata = consumeResult.Message.Headers?
                               .Select(header => new KeyValuePair<string, byte[]>(header.Key, header.GetValueBytes()))
                               .Union(new KeyValuePair<string, byte[]>[]
                               {
                                   new(nameof(consumeResult.Partition),
                                       BitConverter.GetBytes(consumeResult.Partition.Value)),
                                   new(nameof(consumeResult.IsPartitionEOF),
                                       BitConverter.GetBytes(consumeResult.IsPartitionEOF))
                               })
                               .ToImmutableDictionary(pair => pair.Key, pair => pair.Value) ??
                           ImmutableDictionary<string, byte[]>.Empty
            };
        }
    }
}