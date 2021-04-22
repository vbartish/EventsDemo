using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Humanizer;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates;

namespace VBart.EventsDemo.Inventory.Data.Postgres.DataServices
{
    public class AggregateDataService<TAggregate, TMetadata> :
        IAggregateDataService<TAggregate, TMetadata>,
        IAggregateSnapshotDataService<TAggregate, TMetadata>
        where TAggregate : IAggregate<TMetadata>, IMessage, new()
        where TMetadata : class, IMessage, new()
    {
        private readonly IDbAdapter _adapter;
        private readonly JsonFormatter _formatter;
        private readonly JsonParser _parser;

        public AggregateDataService(
            IDbAdapter adapter,
            TypeRegistry typeRegistry)
        {
            _adapter = adapter;
            _formatter = new JsonFormatter(JsonFormatter.Settings.Default.WithTypeRegistry(typeRegistry));
            _parser = new JsonParser(JsonParser.Settings.Default.WithTypeRegistry(typeRegistry));
        }

        Task<TAggregate?> IAggregateDataService<TAggregate, TMetadata>.Find(Guid aggregateUuid) =>
            Find(aggregateUuid, GetTableName());

        Task IAggregateDataService<TAggregate, TMetadata>.Upsert(TAggregate aggregate) =>
            Upsert(aggregate, GetTableName());

        Task IAggregateDataService<TAggregate, TMetadata>.Delete(Guid aggregateUuid) =>
            Delete(aggregateUuid, GetTableName());

        Task<TAggregate?> IAggregateSnapshotDataService<TAggregate, TMetadata>.Find(Guid aggregateUuid) =>
            Find(aggregateUuid, GetTableName(true));

        Task IAggregateSnapshotDataService<TAggregate, TMetadata>.Upsert(TAggregate snapshot) =>
            Upsert(snapshot, GetTableName(true));

        Task IAggregateSnapshotDataService<TAggregate, TMetadata>.Delete(Guid aggregateUuid) =>
            Delete(aggregateUuid, GetTableName(true));

        private async Task<TAggregate?> Find(Guid aggregateUuid, string tableName)
        {
            var query = @$"SELECT payload::text as {nameof(PayloadRecord.Payload)},
                                  metadata_payload::text as {nameof(PayloadRecord.MetadataPayload)}
                            FROM public.{tableName} WHERE {tableName}_uuid = @AggregateUuid";
            var persisted = await _adapter.GetSingleOrDefault<PayloadRecord>(query, new
            {
                AggregateUuid = aggregateUuid
            });

            if (persisted == null)
            {
                return default;
            }

            var aggregate = _parser.Parse<TAggregate>(persisted.Payload);
            aggregate.Metadata = _parser.Parse<TMetadata>(persisted.MetadataPayload);

            return aggregate;
        }

        private async Task Upsert(TAggregate aggregate, string tableName)
        {
            if (EqualityComparer<TAggregate>.Default.Equals(aggregate, default))
            {
                throw new ArgumentNullException(nameof(aggregate));
            }

            var query = $@"INSERT INTO public.{tableName} ({tableName}_uuid, payload, metadata_payload)
                           VALUES (@AggregateUuid, @Payload::jsonb, @MetadataPayload::jsonb)
                           ON CONFLICT ({tableName}_uuid) DO UPDATE SET payload = @Payload::jsonb, metadata_payload = @MetadataPayload::jsonb";

            await _adapter.ExecuteCommand(query, new
            {
                AggregateUuid = Guid.Parse(aggregate.AggregateUuid),
                Payload = _formatter.Format(aggregate),
                MetadataPayload = _formatter.Format(aggregate.Metadata),
            });
        }

        private async Task Delete(Guid aggregateUuid, string tableName)
        {
            var query = $"DELETE FROM public.{tableName} WHERE {tableName}_uuid = @AggregateUuid";
            await _adapter.ExecuteCommand(query, new
            {
                AggregateUuid = aggregateUuid
            });
        }

        private static string GetTableName(bool isSnapshot = false) =>
            typeof(TAggregate).Name.Underscore() + (isSnapshot ? "_snapshot" : string.Empty);
    }
}