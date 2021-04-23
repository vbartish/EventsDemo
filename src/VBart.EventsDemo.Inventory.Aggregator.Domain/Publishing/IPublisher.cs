using System;
using System.Threading.Tasks;
using Google.Protobuf;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Publishing
{
    public interface IPublisher<in TPayload>
        where TPayload : IMessage
    {
        Task Publish(TPayload payload, Guid aggregateUuid);
    }
}