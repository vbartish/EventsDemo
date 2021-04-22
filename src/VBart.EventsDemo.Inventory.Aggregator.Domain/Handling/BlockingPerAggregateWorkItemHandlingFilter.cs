using System;
using System.Collections.Immutable;
using System.Linq;
using VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Handling;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Handling
{
    public class BlockingPerAggregateWorkItemHandlingFilter<TWorkItem> : IWorkItemHandlingFilter<TWorkItem>
        where TWorkItem : WorkItemBase, IAggregateAware
    {
        public bool ShouldProcessWorkItem(TWorkItem item, ImmutableList<TWorkItem> toProcess)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(toProcess));
            }

            if (toProcess == null)
            {
                throw new ArgumentNullException(nameof(toProcess));
            }

            var processingItemIndex = toProcess.FindIndex(matchingItem => item == matchingItem);
            return !toProcess.Where((t, index) =>
                    t.AggregateUuid == item.AggregateUuid &&
                    !t.ProcessedAtUnixUtcTimestamp.HasValue && index < processingItemIndex)
                .Any();
        }
    }
}