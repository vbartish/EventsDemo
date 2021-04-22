using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Events;
using VBart.EventsDemo.Inventory.Aggregator.Domain.Projections;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Handling;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Handling
{
    public class DefaultCdcWorkItemHandler<TKey, TValue> : IWorkItemHandler<CdcProjectionWorkItem>
    {
        private readonly IInboundEventDataService<TKey, TValue> _inboundEventDataService;
        private readonly IProjectionHandlerResolver<TKey, TValue> _projectionHandlerResolver;
        private readonly ILogger<DefaultCdcWorkItemHandler<TKey, TValue>> _logger;

        public DefaultCdcWorkItemHandler(
            IInboundEventDataService<TKey, TValue> inboundEventDataService,
            IProjectionHandlerResolver<TKey, TValue> projectionHandlerResolver,
            ILogger<DefaultCdcWorkItemHandler<TKey, TValue>> logger)
        {
            _inboundEventDataService = inboundEventDataService;
            _projectionHandlerResolver = projectionHandlerResolver;
            _logger = logger;
        }

        public async Task Handle(CdcProjectionWorkItem workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            var inboundEvent = await _inboundEventDataService.FindInboundEvent(workItem.EventUuid);
            if (inboundEvent == null)
            {
                _logger.LogWarning($"Cdc projection work item for aggregate {workItem.AggregateUuid} references non-existing event {workItem.EventUuid}.");
                return;
            }

            var projectionHandler = _projectionHandlerResolver.Resolve(inboundEvent);
            await projectionHandler.Handle(inboundEvent, workItem.AggregateUuid);
        }
    }
}