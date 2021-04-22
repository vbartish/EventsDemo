using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Projections
{
    public interface IProjectionHandlerResolver<TKey, TMessage>
    {
        IProjectionHandler<TKey, TMessage> Resolve(InboundEvent<TKey, TMessage> inboundEvent);
    }
}