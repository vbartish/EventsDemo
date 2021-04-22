using System.Collections.Immutable;

namespace VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models
{
    public class BatchWorkItem<TWorkItem>
        where TWorkItem : IWorkItem
    {
        public ImmutableList<TWorkItem> Batch { get; init; } = ImmutableList<TWorkItem>.Empty;
    }
}