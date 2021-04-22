using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Handling;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;

namespace VBart.EventsDemo.Utils.BackgroundProcessing
{
    public class BackgroundWorker<TWorkItem> : BackgroundService
        where TWorkItem : class, IWorkItem
    {
        private readonly string _name;
        private readonly ILogger<BackgroundWorker<TWorkItem>> _logger;
        private readonly Pipeline<WorkItemBatchContext<TWorkItem>> _pipeline;

        public BackgroundWorker(
            string name,
            ILifetimeScope scope)
        {
            _name = name;
            _pipeline = scope.Resolve<Pipeline<WorkItemBatchContext<TWorkItem>>>();
            _logger = scope.Resolve<ILogger<BackgroundWorker<TWorkItem>>>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Starting {_name} background worker.");

            try
            {
                var context = new WorkItemBatchContext<TWorkItem>
                {
                    WorkerName = _name,
                    StoppingToken = stoppingToken
                };
                await _pipeline
                    .Do(context, scope =>
                    {
                        var initializedContext = scope.Resolve<WorkItemBatchContext<TWorkItem>>();
                        return scope
                            .Resolve<IWorkItemBatchHandler<TWorkItem>>()
                            .Handle(
                                initializedContext.ProcessingWorkItemsBatch ??
                                    throw new InvalidOperationException(
                                        "Background worker context was not initialized."), stoppingToken);
                    });
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WorkItemBatchPipeline execution was gracefully cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical($"{_name} background worker encountered an unexpected error and will be stopped.", ex);
            }

            _logger.LogInformation($"Stopping {_name} background worker.");
        }
    }
}