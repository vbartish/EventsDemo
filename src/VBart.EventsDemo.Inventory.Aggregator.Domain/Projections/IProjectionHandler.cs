using System;
using System.Threading.Tasks;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Projections
{
    public interface IProjectionHandler<TKey, TMessage>
    {
        Task Handle(InboundEvent<TKey, TMessage> inboundEvent, Guid affectedAggregateUuid);
    }
}