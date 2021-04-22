using System.Threading.Tasks;
using Confluent.Kafka;

namespace VBart.EventsDemo.Kafka
{
    public interface IKafkaMessageHandler<TKey, TValue>
    {
        Task Handle(ConsumeResult<TKey,TValue> result);
    }
}