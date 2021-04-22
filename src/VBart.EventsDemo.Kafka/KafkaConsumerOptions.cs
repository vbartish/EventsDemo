using System;
using System.Collections.Generic;
using System.Linq;
using Confluent.Kafka;
using Humanizer;

namespace VBart.EventsDemo.Kafka
{
    public record KafkaConsumerOptions
    {
        /// <summary>
        /// Request broker's supported API versions to adjust functionality to available protocol features. If set to false,
        /// or the ApiVersionRequest fails, the fallback version <see cref="BrokerVersionFallback"/> will be used.
        /// NOTE: Depends on broker version >=0.10.0. If the request is not supported by (an older) broker the <see cref="BrokerVersionFallback"/> fallback is used.
        /// </summary>
        public bool ApiVersionRequest { get; set; } = true;
        /// <summary>
        /// Older broker versions (before 0.10.0) provide no way for a client to query for supported protocol features
        /// (<see cref="ApiVersionRequest"/>) making it impossible for the client to know what features it may use.
        /// As a workaround a user may set this property to the expected broker version and the client will automatically adjust
        /// its feature set accordingly if the ApiVersionRequest fails (or is disabled). The fallback broker version will be
        /// used for <see cref="VersionFallbackMs"/>. Valid values are: 0.9.0, 0.8.2, 0.8.1, 0.8.0. Any other value >= 0.10,
        /// such as 0.10.2.1, enables ApiVersionRequests.
        /// </summary>
        public string BrokerVersionFallback { get; set; } = "0.10.0.0";
        /// <summary>
        /// Dictates how long the <see cref="BrokerVersionFallback"/> is used in the case the ApiVersionRequest fails.
        /// NOTE: The ApiVersionRequest is only issued when a new connection to the broker is made (such as after an upgrade).
        /// </summary>
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