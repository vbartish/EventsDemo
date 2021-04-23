using System;
using System.Collections.Generic;
using System.Linq;
using Confluent.Kafka;
using Humanizer;

namespace VBart.EventsDemo.Kafka
{
    public record KafkaConsumerOptions
    {
        public bool ApiVersionRequest { get; set; } = true;

        public string BrokerVersionFallback { get; set; } = "0.10.0.0";

        public int VersionFallbackMs { get; set; }
        public string BootstrapServers { get; set; } = string.Empty;

        public string ConsumerGroupId { get; set;} = string.Empty;

        public string Topics { get; set;} = string.Empty;

        public int MaxPollIntervalMs { get; set;} = 300_000;

        public bool AutoCreateTopics { get; set;} = false;

        public bool EnableAutoCommit { get; set;} = false;

        public AutoOffsetReset AutoOffsetReset { get; set;} = AutoOffsetReset.Earliest;

        public SaslMechanism SaslMechanism { get; set;} = SaslMechanism.Plain;

        public SecurityProtocol SecurityProtocol { get; set;} = SecurityProtocol.Plaintext;

        public IEnumerable<string> TopicsList =>
            Topics.Trim().Split(',').Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim());
        public Dictionary<string, string> ToConfluentConfiguration(Action<Dictionary<string,string>>? confluentConfigurationOverride = null)
        {
            var kafkaConfiguration = new Dictionary<string, string>
            {
                { "api.version.request", ApiVersionRequest.ToString() },
                { "broker.version.fallback", BrokerVersionFallback },
                { "api.version.fallback.ms", VersionFallbackMs.ToString() },
                { "sasl.mechanisms", SaslMechanism.ToString() },
                { "security.protocol", SecurityProtocol.ToString().Underscore().ToUpper() },
                { "bootstrap.servers", BootstrapServers },
                { "allow.auto.create.topics", AutoCreateTopics.ToString() },
                { "group.id", ConsumerGroupId },
                { "auto.offset.reset", AutoOffsetReset.ToString() },
                { "enable.auto.commit", EnableAutoCommit.ToString() },
                { "max.poll.interval.ms", MaxPollIntervalMs.ToString() }
            };

            confluentConfigurationOverride?.Invoke(kafkaConfiguration);

            return kafkaConfiguration;
        }
    }
}