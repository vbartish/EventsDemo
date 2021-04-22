using System.Collections.Immutable;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;

namespace VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Handling
{
    public interface IWorkItemHandlingFilter<TWorkItem>
        where TWorkItem : IWorkItem
    {
        bool ShouldProcessWorkItem(TWorkItem item, ImmutableList<TWorkItem> toProcess);
    }
}
