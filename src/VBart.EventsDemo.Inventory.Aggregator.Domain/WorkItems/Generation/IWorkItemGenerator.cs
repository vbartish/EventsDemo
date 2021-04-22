using System.Collections.Generic;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain.WorkItems.Generation
{
    public interface IWorkItemGenerator<in TSource, TWorkItem>
    where TWorkItem : IWorkItem
    {
        List<TWorkItem> GenerateFromSource(TSource source);
    }
}