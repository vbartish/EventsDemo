using System.Collections.Generic;
using Confluent.Kafka;

namespace VBart.EventsDemo.Kafka
{
    public record KafkaProducerOptions
    {
        public bool ApiVersionRequest { get; set; } = true;

        public string BrokerVersionFallback { get; set; } = "0.10.0.0";

        public int VersionFallbackMs { get; set; }

        public string BootstrapServers { get; set; } = string.Empty;

        public Partitioner Partitioner { get; set; } = Partitioner.Consistent;

        public SaslMechanism SaslMechanism { get; set;} = SaslMechanism.Plain;

        public SecurityProtocol SecurityProtocol { get; set;} = SecurityProtocol.Plaintext;

        public bool AutoCreateTopics { get; set;} = false;
        public int MaxInFlight { get; set; } = 1;

        public int MaxRetryCount { get; set; } = 10;

        public int RetryWaitTimeInMs { get; set; } = 100;

        public Dictionary<string, string> ToConfluentProducerConfiguration()
        {
            var kafkaConfiguration = new Dictionary<string, string>
            {
                { "api.version.request", ApiVersionRequest.ToString() },
                { "broker.version.fallback", BrokerVersionFallback },
                { "api.version.fallback.ms", VersionFallbackMs.ToString() },
                { "sasl.mechanisms", SaslMechanism.ToString() },
                { "security.protocol", SecurityProtocol.ToString() },
                { "bootstrap.servers", BootstrapServers },
                { "partitioner", Partitioner.ToString().ToLower() },
                { "allow.auto.create.topics", AutoCreateTopics.ToString() },
                { "max.in.flight",  MaxInFlight.ToString() },
                { "message.send.max.retries", MaxRetryCount.ToString() },
                { "retry.backoff.ms", RetryWaitTimeInMs.ToString() },
            };

            return kafkaConfiguration;
        }
    }
}