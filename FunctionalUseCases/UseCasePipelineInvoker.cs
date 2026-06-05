using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace FunctionalUseCases;

internal static class UseCasePipelineInvoker<TResult>
    where TResult : notnull
{
    private static readonly ConcurrentDictionary<Type, IInvoker> Invokers = new();

    public static Task<ExecutionResult<TResult>> ExecuteAsync(
        IServiceProvider serviceProvider,
        IUseCaseParameter<TResult> useCaseParameter,
        IEnumerable<object>? perCallBehaviors,
        IExecutionScope scope,
        CancellationToken cancellationToken)
    {
        var parameterType = useCaseParameter.GetType();
        var invoker = Invokers.GetOrAdd(parameterType, static type =>
        {
            var invokerType = typeof(UseCasePipelineInvoker<,>).MakeGenericType(type, typeof(TResult));
            return (IInvoker)Activator.CreateInstance(invokerType)!;
        });

        return invoker.ExecuteAsync(
            serviceProvider,
            useCaseParameter,
            perCallBehaviors ?? [],
            scope,
            cancellationToken);
    }

    internal interface IInvoker
    {
        Task<ExecutionResult<TResult>> ExecuteAsync(
            IServiceProvider serviceProvider,
            IUseCaseParameter<TResult> useCaseParameter,
            IEnumerable<object> perCallBehaviors,
            IExecutionScope scope,
            CancellationToken cancellationToken);
    }

}

internal sealed class UseCasePipelineInvoker<TUseCaseParameter, TResult> :
    UseCasePipelineInvoker<TResult>.IInvoker
    where TUseCaseParameter : IUseCaseParameter<TResult>
    where TResult : notnull
{
    public async Task<ExecutionResult<TResult>> ExecuteAsync(
        IServiceProvider serviceProvider,
        IUseCaseParameter<TResult> useCaseParameter,
        IEnumerable<object> perCallBehaviors,
        IExecutionScope scope,
        CancellationToken cancellationToken)
    {
        var parameter = (TUseCaseParameter)useCaseParameter;
        var useCase = serviceProvider.GetService<IUseCase<TUseCaseParameter, TResult>>();
        if (useCase is null)
        {
            return Execution.Failure<TResult>(
                $"No use case registered for parameter type '{typeof(TUseCaseParameter).Name}'");
        }

        var applicablePerCallBehaviors = new List<IExecutionBehavior<TUseCaseParameter, TResult>>();
        foreach (var behavior in perCallBehaviors)
        {
            if (behavior is OpenGenericBehaviorDescriptor descriptor)
            {
                try
                {
                    var concreteType = descriptor.OpenGenericType.MakeGenericType(
                        typeof(TUseCaseParameter),
                        typeof(TResult));
                    var resolvedBehavior = serviceProvider.GetService(concreteType);
                    if (resolvedBehavior is not IExecutionBehavior<TUseCaseParameter, TResult> typedBehavior)
                    {
                        return Execution.Failure<TResult>(
                            $"Failed to resolve open generic behavior {descriptor.OpenGenericType.Name}<{typeof(TUseCaseParameter).Name},{typeof(TResult).Name}>: Service not registered");
                    }

                    applicablePerCallBehaviors.Add(typedBehavior);
                }
                catch (Exception ex)
                {
                    return Execution.Failure<TResult>(
                        $"Failed to resolve open generic behavior {descriptor.OpenGenericType.Name}: {ex.Message}",
                        ex);
                }
            }
            else if (behavior is IExecutionBehavior<TUseCaseParameter, TResult> typedBehavior)
            {
                applicablePerCallBehaviors.Add(typedBehavior);
            }
        }

        var behaviors = applicablePerCallBehaviors
            .Concat(serviceProvider.GetServices<IExecutionBehavior<TUseCaseParameter, TResult>>())
            .ToArray();

        PipelineBehaviorDelegate<TResult> pipeline = () =>
            useCase.ExecuteAsync(parameter, cancellationToken);

        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = pipeline;
            pipeline = behavior is IScopedExecutionBehavior<TUseCaseParameter, TResult> scopedBehavior
                ? () => scopedBehavior.ExecuteAsync(parameter, scope, next, cancellationToken)
                : () => behavior.ExecuteAsync(parameter, next, cancellationToken);
        }

        return await pipeline().ConfigureAwait(false);
    }
}
