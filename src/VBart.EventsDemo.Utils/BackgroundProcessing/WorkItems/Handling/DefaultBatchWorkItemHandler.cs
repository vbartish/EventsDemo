using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Persistence;
using VBart.EventsDemo.Utils.BackoffProviders;

namespace VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Handling
{
    public class DefaultBatchWorkItemHandler<TWorkItem> : IWorkItemBatchHandler<TWorkItem>
        where TWorkItem : WorkItemBase
    {
        private readonly Pipeline<TWorkItem> _pipeline;
        private readonly IWorkItemDataService<TWorkItem> _workItemDataService;
        private readonly ISystemClock _systemClock;
        private readonly ILogger<DefaultBatchWorkItemHandler<TWorkItem>> _logger;

        public DefaultBatchWorkItemHandler(
            Pipeline<TWorkItem> pipeline,
            IWorkItemDataService<TWorkItem> workItemDataService,
            ISystemClock systemClock,
            ILogger<DefaultBatchWorkItemHandler<TWorkItem>> logger)
        {
            _pipeline = pipeline;
            _workItemDataService = workItemDataService;
            _systemClock = systemClock;
            _logger = logger;
        }

        public async Task Handle(BatchWorkItem<TWorkItem> workItem, CancellationToken cancellationToken)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            var toProcess = workItem.Batch;

            for (var index = 0; index < toProcess.Count; index++)
            {
                var item = toProcess[index];
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await _pipeline
                    .DoWithFallback(
                        item,
                        async scope =>
                        {
                            var filters = scope.ResolveOptional<IEnumerable<IWorkItemHandlingFilter<TWorkItem>>>();

                            if (filters?.All(filter => filter.ShouldProcessWorkItem(item, toProcess)) ?? true)
                            {
                                await scope.Resolve<IWorkItemHandler<TWorkItem>>().Handle(item);
                                OnSuccess();
                                return;
                            }

                            _logger.LogDebug($"Work item {item.Uuid} skipped processing due to filters.");
                        },
                        (scope, exception) =>
                        {
                            OnFailure(scope, exception);
                            return Task.CompletedTask;
                        });

                void OnSuccess()
                {
                    var processedItem = item with
                    {
                        ProcessedAtUnixUtcTimestamp = _systemClock.UtcNow.ToUnixTimeMilliseconds()
                    };

                    _logger.LogDebug($"Work item {processedItem.Uuid} processed successfully work item.");
                    toProcess = toProcess.SetItem(index, processedItem);
                }

                void OnFailure(ILifetimeScope scope, Exception exception)
                {
                    var backoffDelayProvider = scope.Resolve<IBackoffDelayProvider>();
                    var options = scope.Resolve<WorkItemProcessingOptions>();
                    var failedItem = item with
                    {
                        RetryCounter = item.RetryCounter + 1,
                        NextRetryUnixUtcTimestamp = backoffDelayProvider
                            .GetBackoffDelayMs(
                                item.RetryCounter + 1,
                                options.DelayedProcessingInitialDelayMs,
                                options.DelayedProcessingMaxDelayMs) + _systemClock.UtcNow.ToUnixTimeMilliseconds()
                    };
                    _logger.LogError(exception, $"Failed to process work item {failedItem.Uuid}.");

                    toProcess = toProcess!.SetItem(index, failedItem);
                }
            }

            await _workItemDataService.UpsertWorkItems(toProcess.ToList());
        }
    }
}