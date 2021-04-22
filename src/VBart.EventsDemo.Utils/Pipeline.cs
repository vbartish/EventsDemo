using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using System.Transactions;
using Autofac;

namespace VBart.EventsDemo.Utils
{
    public class Pipeline<TContext>
        where TContext : class
    {
        public delegate Task Execute(ILifetimeScope scope);

        public delegate Task<TResponse> Execute<TResponse>(ILifetimeScope scope) where TResponse : notnull;

        public delegate Execute Step(Execute next);

        public Pipeline(ILifetimeScope scope)
        {
            Scope = scope;
        }

        public ImmutableStack<Step> Steps { get; init; } = ImmutableStack<Step>.Empty;

        protected ILifetimeScope Scope { get; }

        public Pipeline<TContext> With(Func<ILifetimeScope, Execute, Task> step) =>
            AddStep(next => scope => step(scope, next));


        public Pipeline<TContext> WithNestedScope(Action<ContainerBuilder> configurationAction) =>
            With(async (scope,
                next) =>
            {
                var childScope = scope.BeginLifetimeScope(configurationAction);
                try
                {
                    await next(childScope);
                }
                finally
                {
                    if (childScope != scope)
                    {
                        await childScope.DisposeAsync();
                    }
                }
            });

        public Pipeline<TContext> WithNewAmbientTransaction() =>
            With(async (scope, next) =>
            {
                using var transactionScope =
                    new TransactionScope(
                        TransactionScopeOption.RequiresNew,
                        new TransactionOptions
                        {
                            IsolationLevel = IsolationLevel.ReadCommitted
                        },
                        TransactionScopeAsyncFlowOption.Enabled);
                await next(scope);
                transactionScope.Complete();
            });

        public Task Do(TContext context,
            Func<Task> act) => Do(context, _ => act());

        public async Task Do(TContext context,
            Execute act)
        {
            await using var nestedScopeWithContext
                = Scope.BeginLifetimeScope(builder => builder.RegisterInstance(context)) ??
                  throw new InvalidOperationException("Pipeline scope was not initialized.");

            var pipe = BuildPipeline(act, (previous,
                toDo) => previous(toDo));
            await pipe(nestedScopeWithContext);
        }

        public Task<TResponse> Do<TResponse>(TContext context,
            Func<Task<TResponse>> act) where TResponse : notnull
            => Do(context, _ => act());

        public async Task<TResponse> Do<TResponse>(TContext context,
            Execute<TResponse> act) where TResponse : notnull
        {
            await using var nestedScopeWithContext =
                Scope?.BeginLifetimeScope(builder => builder.RegisterInstance(context)) ??
                throw new InvalidOperationException("Pipeline scope was not initialized.");

            var pipe = BuildPipeline(
                act,
                (previous,
                    toDo) => async scp =>
                {
                    var response = default(TResponse);
                    await previous(async s => response = await toDo(s))(scp);
                    return response!;
                });

            return await pipe(nestedScopeWithContext);
        }

        public async Task DoWithFallback(
            TContext context,
            Execute act,
            Func<ILifetimeScope, Exception, Task> onFallback) =>
            await Do(context, async scope =>
            {
                try
                {
                    await act(scope);
                }
                catch (Exception exception)
                {
                    await onFallback(scope, exception);
                }
            });

        private TAction BuildPipeline<TAction>(
            TAction innermostAction,
            Func<Step, TAction, TAction> stepMutation)
            where TAction : Delegate
        {
            var toDo = innermostAction;

            var remaining = Steps;
            while (!remaining.IsEmpty)
            {
                remaining = remaining.Pop(out var previous);
                toDo = stepMutation(previous, toDo);
            }

            return toDo;
        }

        private Pipeline<TContext> AddStep(Step step)
        {
            return new(Scope!)
            {
                Steps = Steps.Push(step)
            };
        }
    }
}