using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;

namespace VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Persistence
{
    public interface IWorkItemDataService<TWorkItem>
        where TWorkItem : IWorkItem
    {
        Task UpsertWorkItems(List<TWorkItem> workItems);

        Task<List<TWorkItem>> PickABatchWithDistroLock(int workItemsBatchSize, int scaleFactor);

        Task<int> GetUnprocessedCount();

        Task<int> DeleteProcessedWorkItems(DateTimeOffset processedBefore);
    }
}