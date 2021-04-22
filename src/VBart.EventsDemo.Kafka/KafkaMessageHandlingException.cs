using System;

namespace VBart.EventsDemo.Kafka
{
    public sealed class KafkaMessageHandlingException : Exception
    {
        public KafkaMessageHandlingException()
        {
        }

        public KafkaMessageHandlingException(string? message)
            : base(message)
        {
        }

        public KafkaMessageHandlingException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}