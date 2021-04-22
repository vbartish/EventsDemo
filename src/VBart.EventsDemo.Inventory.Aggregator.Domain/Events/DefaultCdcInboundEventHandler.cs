using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Persistence;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Events
{
    public class DefaultCdcInboundEventHandler : IInboundEventHandler<Guid, JObject>
    {
        private readonly IInboundEventDataService<Guid, JObject> _inboundEventDataService;
        private readonly IWorkItemGenerator<
            (InboundEvent<Guid, JObject> PersistedEvent, Guid PersistedEventUuid),
            CdcProjectionWorkItem> _inboundEventWorkItemGenerator;
        private readonly IWorkItemDataService<CdcProjectionWorkItem> _workItemDataService;
        private readonly ILogger<DefaultCdcInboundEventHandler> _logger;

        public DefaultCdcInboundEventHandler(
            IInboundEventDataService<Guid, JObject> inboundEventDataService,
            IWorkItemGenerator<
                (InboundEvent<Guid, JObject> PersistedEvent, Guid PersistedEventUuid),
                CdcProjectionWorkItem> inboundEventWorkItemGenerator,
            IWorkItemDataService<CdcProjectionWorkItem> workItemDataService,
            ILogger<DefaultCdcInboundEventHandler> logger)
        {
            _inboundEventDataService = inboundEventDataService;
            _inboundEventWorkItemGenerator = inboundEventWorkItemGenerator;
            _workItemDataService = workItemDataService;
            _logger = logger;
        }

        public async Task Handle(InboundEvent<Guid, JObject> inboundEvent)
        {
            if (inboundEvent == null)
            {
                throw new ArgumentNullException(nameof(inboundEvent));
            }

            _logger.LogInformation($"Inbound event {inboundEvent.EventKey} processing started.");
            var persistedEventUuid = await _inboundEventDataService.PersistInboundEvent(inboundEvent);
            var workItemsForEvent = _inboundEventWorkItemGenerator.GenerateFromSource((PersistedEvent: inboundEvent, PersistedEventUuid: persistedEventUuid));
            await _workItemDataService.UpsertWorkItems(workItemsForEvent);
            _logger.LogInformation($"Inbound event {inboundEvent.EventKey} processing done.");
        }
    }
}