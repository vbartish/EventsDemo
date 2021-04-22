using System;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using VBart.EventsDemo.Utils.BackgroundProcessing;
using VBart.EventsDemo.Utils.BackgroundProcessing.Options;
using VBart.EventsDemo.Utils.BackgroundProcessing.WorkItems.Models;
using VBart.EventsDemo.Utils.BackoffProviders;
using Polly;

namespace VBart.EventsDemo.Utils
{
    public static class PipelineExtensions
    {
        public static Pipeline<WorkItemBatchContext<TWorkItem>> WithRetryForever<TWorkItem>(this Pipeline<WorkItemBatchContext<TWorkItem>> pipe)
            where TWorkItem : class, IWorkItem
        {
            return pipe.With(async (scope,
                next) =>
            {
                var stoppingToken = scope.Resolve<WorkItemBatchContext<TWorkItem>>().StoppingToken;
                var context = scope.Resolve<WorkItemBatchContext<TWorkItem>>();
                var options = scope.Resolve<BackgroundWorkerOptions>();
                var logger = scope.Resolve<ILogger<Pipeline<WorkItemBatchContext<TWorkItem>>>>();
                var backoffProvider = scope.Resolve<IBackoffDelayProvider>();
                var retryCount = 0;
                var policy = Policy<(bool CancellationRequested, int AttemptedWorkItems)>
                    .HandleResult(shouldContinueResult => !shouldContinueResult.CancellationRequested)
                    .WaitAndRetryForeverAsync(
                        (_,
                            result,
                            _) =>
                        {
                            if (result.Result.AttemptedWorkItems != 0)
                            {
                                retryCount = 0;
                                return TimeSpan.Zero;
                            }

                            retryCount++;
                            var delayMs = backoffProvider.GetBackoffDelayMs(
                                retryCount,
                                options.NoWorkInitialDelayMs,
                                options.NoWorkMaxDelayMs);
                            return TimeSpan.FromMilliseconds(delayMs);
                        },
                        (_,
                            _,
                            sleepDuration,
                            _) =>
                        {
                            logger.LogInformation(
                                $"No planned work items for {context.WorkerName} on retry attempt {retryCount}, sleeping for {sleepDuration}");
                            return Task.CompletedTask;
                        });
                await policy.ExecuteAsync(
                    async token =>
                    {
                        (bool CancellationRequested, int AttemptedWorkItems) result;
                        if (token.IsCancellationRequested)
                        {
                            result = (true, 0);
                            return result;
                        }

                        await next(scope);
                        var pipelineContext = scope.Resolve<WorkItemBatchContext<TWorkItem>>();
                        result = (false, pipelineContext.ProcessingWorkItemsBatch?.Batch.Count ?? 0);
                        return result;
                    },
                    stoppingToken);
            });
        }

        public static Pipeline<WorkItemBatchContext<TWorkItem>> InWorkItemsBatchContext<TWorkItem>(
            this Pipeline<WorkItemBatchContext<TWorkItem>> pipe,
            Func<ILifetimeScope, Task<BatchWorkItem<TWorkItem>>> batchWorkItemProviderFunc)
            where TWorkItem : class, IWorkItem
        {
            return pipe.With(async (scope,
                next) =>
            {
                var context = scope.Resolve<WorkItemBatchContext<TWorkItem>>();
                context.ProcessingWorkItemsBatch = (BatchWorkItem<TWorkItem>?) await batchWorkItemProviderFunc(scope);
                await next(scope);
            });
        }
    }
}