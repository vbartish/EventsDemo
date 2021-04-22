using System.Threading;
using System.Threading.Tasks;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;

namespace VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Handling
{
    public interface IWorkItemBatchHandler<TWorkItem>
    where TWorkItem : IWorkItem
    {
        Task Handle(BatchWorkItem<TWorkItem> workItem, CancellationToken cancellationToken);
    }
}
