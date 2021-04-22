using System;
using Autofac;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Projections
{
    public class SourceBasedProjectionHandlerResolver<TKey, TMessage> : IProjectionHandlerResolver<TKey, TMessage>
    {
        private readonly ILifetimeScope _scope;

        public SourceBasedProjectionHandlerResolver(ILifetimeScope scope)
        {
            _scope = scope;
        }

        public IProjectionHandler<TKey, TMessage> Resolve(InboundEvent<TKey, TMessage> inboundEvent)
        {
            if (inboundEvent == null)
            {
                throw new ArgumentNullException(nameof(inboundEvent));
            }

            return _scope.ResolveKeyed<IProjectionHandler<TKey, TMessage>>(inboundEvent.Source);
        }
    }
}