using System;
using System.Threading.Tasks;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Events
{
    public interface IInboundEventDataService<TKey, TMessage>
    {
        Task<Guid> PersistInboundEvent(InboundEvent<TKey, TMessage> consumedEvent);

        Task<InboundEvent<TKey, TMessage>?> FindInboundEvent(Guid eventUuid);
    }
}