using System;
using System.Linq;
using Autofac;
using Google.Protobuf.Reflection;
using VBart.EventsDemo.Grpc.ValueObjects;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain
{
    public static class ProtobufExtensions
    {
        public static string? GetValueOrDefault(
            this MetaData metaData,
            string key,
            string? defaultValue)
        {
            var found = metaData.Entries.SingleOrDefault(entry =>
                string.Equals(entry.Key, key, StringComparison.Ordinal));
            return found?.Value ?? defaultValue;
        }

        public static void Upsert(
            this MetaData metaData,
            string key,
            string? value)
        {
            var found = metaData.Entries.FirstOrDefault(entry =>
                string.Equals(entry.Key, key, StringComparison.Ordinal));

            if (found == null)
            {
                metaData.Entries.Add(new MetaData.Types.MetadataEntry
                {
                    Key = key,
                    Value = value,
                });
            }
            else
            {
                found.Value = value;
            }
        }

        public static ContainerBuilder AddTypeRegistry(this ContainerBuilder builder)
        {
            builder.RegisterInstance(TypeRegistry.FromFiles(
                AggregatesReflection.Descriptor,
                ValueObjectsReflection.Descriptor));
            return builder;
        }
    }
}