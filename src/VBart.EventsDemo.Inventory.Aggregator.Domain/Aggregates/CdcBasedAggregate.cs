using System;
using System.Globalization;
using Humanizer;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.LogSequenceNumbers;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates
{
    public abstract class CdcBasedAggregate : IAggregate<MetaData>
    {
        public abstract string AggregateUuid { get; set; }

        public MetaData Metadata { get; set; } = new ();

        public uint InternalRevision
        {
            get
            {
                var stringValue = Metadata.GetValueOrDefault(nameof(InternalRevision).Underscore().ToLower(), "0");
                return stringValue == null ? 0 : uint.Parse(stringValue);
            }

            set => Metadata.Upsert(nameof(InternalRevision).Underscore().ToLower(), value.ToString(CultureInfo.InvariantCulture));
        }

        public uint LastPublishedRevision
        {
            get
            {
                var stringValue = Metadata.GetValueOrDefault(nameof(LastPublishedRevision).Underscore().ToLower(), "0");
                return stringValue == null ? 0 : uint.Parse(stringValue);
            }

            set => Metadata.Upsert(nameof(LastPublishedRevision).Underscore().ToLower(), value.ToString(CultureInfo.InvariantCulture));
        }

        public long ChangedAtUnixUtcTimestamp
        {
            get
            {
                var stringValue = Metadata.GetValueOrDefault(nameof(ChangedAtUnixUtcTimestamp).Underscore().ToLower(), "0");
                return stringValue == null ? 0 : long.Parse(stringValue);
            }

            set => Metadata.Upsert(nameof(ChangedAtUnixUtcTimestamp).Underscore().ToLower(), value.ToString());
        }

        public TLastProjectedMetadata? GetLastProjectedMetadata<TProjectedModel, TLastProjectedMetadata>(string suffix = "")
            where TLastProjectedMetadata : class
        {
            EnsureProjectedMetadataType<TLastProjectedMetadata>();
            var key = typeof(TProjectedModel).Name.Underscore().ToLower();
            var commitLsn = GetLastProjectedCommitLsn(key + suffix);

            if (commitLsn == null)
            {
                return null;
            }

            var changeLsn = GetLastProjectedChangeLsn(key + suffix);
            var result = new LsnAggregate(commitLsn, changeLsn ?? commitLsn);
            return result as TLastProjectedMetadata;
        }

        public void SetLastProjectedMetadata<TProjectedModel, TLastProjectedMetadata>(TLastProjectedMetadata value, string suffix = "")
        {
            EnsureProjectedMetadataType<TLastProjectedMetadata>();
            var key = typeof(TProjectedModel).Name.Underscore().ToLower() + suffix;
            var castedValue = value as LsnAggregate;
            SetLastProjectedCommitLsn(key, castedValue!.CommitLsn);
            if (castedValue!.ChangeLsn != null)
            {
                SetLastProjectedChangeLsn(key, castedValue!.ChangeLsn);
            }
        }

        public abstract bool IsCohesive();

        private void EnsureProjectedMetadataType<TProjectedMetadata>()
        {
            if (typeof(TProjectedMetadata) != typeof(LsnAggregate))
            {
                throw new InvalidOperationException("Aggregation of non-CDC events is not supported.");
            }
        }

        private Lsn GetLastProjectedCommitLsn(string key)
        {
            var stringValue = Metadata.GetValueOrDefault($"last_projected_commit_lsn_{key}", null);
            return stringValue == null ? new Lsn(0,0,0) : new Lsn(stringValue);
        }

        private void SetLastProjectedCommitLsn(string key, Lsn value) =>
            Metadata.Upsert($"last_projected_commit_lsn_{key}", value.StringValue);

        private Lsn GetLastProjectedChangeLsn(string key)
        {
            var stringValue = Metadata.GetValueOrDefault($"last_projected_change_lsn_{key}", null);
            return stringValue == null ? new Lsn(0,0,0) : new Lsn(stringValue);
        }

        private void SetLastProjectedChangeLsn(string key, Lsn value) =>
            Metadata.Upsert($"last_projected_change_lsn_{key}", value.StringValue);
    }
}