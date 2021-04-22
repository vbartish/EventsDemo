using System.Threading.Tasks;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;

namespace VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Handling
{
    public interface IWorkItemHandler<in TWorkItem>
    where TWorkItem : IWorkItem
    {
        Task Handle(TWorkItem workItem);
    }
}
