using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;

namespace VBart.EventsDemo.Inventory.Data.Postgres.DataServices
{
    public class CdcInboundEventDataService : IInboundEventDataService<Guid, JObject>
    {
        private readonly IDbAdapter _adapter;

        public CdcInboundEventDataService(IDbAdapter adapter)
        {
            _adapter = adapter;
        }

        public async Task<Guid> PersistInboundEvent(InboundEvent<Guid, JObject> consumedEvent)
        {
            if (consumedEvent == null)
            {
                throw new ArgumentNullException(nameof(consumedEvent));
            }

            var insertQuery = @$"INSERT INTO public.cdc_inbound_event
                (source_offset, source, event_key, event_message, event_unix_utc_timestamp) 
                VALUES
                (@{nameof(EventRecord.SourceOffset)}, @{nameof(EventRecord.Source)}, @{nameof(EventRecord.EventKey)}
                , @{nameof(EventRecord.EventMessage)}::jsonb, @{nameof(EventRecord.EventUnixUtcTimestamp)})
                RETURNING event_uuid;";
            var insertedUuid = await _adapter.ExecuteScalarCommand<Guid>(insertQuery, new EventRecord(consumedEvent));

            if (consumedEvent.Metadata.Count == 0)
            {
                return insertedUuid;
            }

            var builder = new StringBuilder();
            builder.AppendLine("INSERT INTO public.cdc_inbound_event_header (event_uuid, header_key, header_value) VALUES");
            var paramsDictionary = new Dictionary<string, object>();
            var index = 0;
            foreach (var tuple in consumedEvent.Metadata)
            {
                builder.AppendLine($"(@EventUuid{index}, @HeaderKey{index}, @HeaderValue{index})");
                paramsDictionary.Add($"@EventUuid{index}", insertedUuid);
                paramsDictionary.Add($"@HeaderKey{index}", tuple.Key);
                paramsDictionary.Add($"@HeaderValue{index}", tuple.Value);
                builder.Append(index != consumedEvent.Metadata.Count - 1 ? ',' : ';');
                index++;
            }

            await _adapter.ExecuteCommand(builder.ToString(), paramsDictionary);
            return insertedUuid;
        }

        public async Task<InboundEvent<Guid, JObject>?> FindInboundEvent(Guid eventUuid)
        {
            var selectEventQuery =
                @$"SELECT 
                    source_offset as {nameof(EventRecord.SourceOffset)}
                    , source as {nameof(EventRecord.Source)}
                    , event_key as {nameof(EventRecord.EventKey)}
                    , event_message as {nameof(EventRecord.EventMessage)}
                    , event_unix_utc_timestamp as {nameof(EventRecord.EventUnixUtcTimestamp)} 
                  FROM public.cdc_inbound_event
                  WHERE event_uuid = @EventUuid";

            const string selectHeaderQuery =
                @"SELECT header_key as HeaderKey, header_value as HeaderValue
                  FROM public.cdc_inbound_event_header
                  WHERE event_uuid = @EventUuid";

            var consumedEvent =
                await _adapter.GetSingleOrDefault<EventRecord>(selectEventQuery, new { EventUuid = eventUuid });

            if (consumedEvent == null)
            {
                return null;
            }

            var additionalMetadata = (await _adapter.GetMany<MetadataRecord>(
                    selectHeaderQuery,
                    new { EventUuid = eventUuid }))
                .ToImmutableDictionary(header => header.HeaderKey, header => header.HeaderValue);

            return consumedEvent.ToInboundEvent(additionalMetadata);
        }

        private record EventRecord
        {
            private EventRecord()
            {
            }

            public EventRecord(InboundEvent<Guid, JObject> inboundEvent)
            {
                Source = inboundEvent.Source;
                EventKey = inboundEvent.EventKey;
                EventMessage = inboundEvent.EventMessage?.ToString() ?? string.Empty;
                EventUnixUtcTimestamp = inboundEvent.UnixUtcTimestampMs;
                SourceOffset = inboundEvent.Offset;
            }

            public string Source { get; init; } = string.Empty;
            public long SourceOffset { get; init; }
            public long EventUnixUtcTimestamp { get; init; }
            public Guid EventKey { get; init; }
            public string EventMessage { get; init; } = string.Empty;

            public InboundEvent<Guid, JObject> ToInboundEvent(
                ImmutableDictionary<string, byte[]> additionalMetadata) =>
                new()
                {
                    Source = Source,
                    Offset = SourceOffset,
                    Metadata = additionalMetadata,
                    EventKey = EventKey,
                    EventMessage = JObject.Parse(EventMessage),
                    UnixUtcTimestampMs = EventUnixUtcTimestamp
                };
        }

        private record MetadataRecord
        {
            public string HeaderKey { get; init; } = string.Empty;
            public byte[] HeaderValue { get; init; } = Array.Empty<byte>();
        }
    }
}