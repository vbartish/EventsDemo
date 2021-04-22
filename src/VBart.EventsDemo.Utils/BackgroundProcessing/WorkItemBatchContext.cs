using System.Threading;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;

namespace VBart.EventsDemo.Utils.BackgroundProcessing
{
    public class WorkItemBatchContext<TWorkItem>
        where TWorkItem : class, IWorkItem
    {
        public string WorkerName { get; init; } = string.Empty;

        public BatchWorkItem<TWorkItem>? ProcessingWorkItemsBatch { get; set; }

        public CancellationToken StoppingToken { get; init; } = CancellationToken.None;
    }
}