using System.Threading.Tasks;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Events
{
    public interface IInboundEventHandler<TKey, TValue>
    {
        Task Handle(InboundEvent<TKey, TValue> inboundEvent);
    }
}