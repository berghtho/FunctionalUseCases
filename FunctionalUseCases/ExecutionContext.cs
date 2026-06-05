using Microsoft.Extensions.DependencyInjection;

namespace FunctionalUseCases;

/// <summary>
/// Represents an open generic behavior type that will be resolved at execution time.
/// </summary>
internal class OpenGenericBehaviorDescriptor
{
    public Type OpenGenericType { get; }

    public OpenGenericBehaviorDescriptor(Type openGenericType)
    {
        OpenGenericType = openGenericType ?? throw new ArgumentNullException(nameof(openGenericType));
    }
}

/// <summary>
/// Implementation of execution context that manages per-call behaviors.
/// </summary>
/// <typeparam name="TResult">The result type of the execution.</typeparam>
internal class ExecutionContext<TResult> : IExecutionContext<TResult>
    where TResult : notnull
{
    private readonly IUseCaseDispatcher _dispatcher;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<object> _perCallBehaviors;

    public ExecutionContext(IUseCaseDispatcher dispatcher, IServiceProvider serviceProvider)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _perCallBehaviors = new List<object>();
    }

    internal ExecutionContext(IUseCaseDispatcher dispatcher, IServiceProvider serviceProvider, List<object> existingBehaviors)
    {
        _dispatcher = dispatcher;
        _serviceProvider = serviceProvider;
        _perCallBehaviors = new List<object>(existingBehaviors);
    }



    public IExecutionContext<TResult> WithBehavior(Type behaviorType)
    {
        if (behaviorType == null)
        {
            throw new ArgumentNullException(nameof(behaviorType));
        }

        if (!behaviorType.IsGenericTypeDefinition)
        {
            throw new ArgumentException("Behavior type must be an open generic type definition (e.g., typeof(MyBehavior<,>))", nameof(behaviorType));
        }

        // Store the open generic type - we'll resolve it when we know the concrete parameter and result types
        return WithBehavior(new OpenGenericBehaviorDescriptor(behaviorType));
    }

    public IExecutionContext<TResult> WithBehavior(object behavior)
    {
        if (behavior == null)
        {
            throw new ArgumentNullException(nameof(behavior));
        }

        var newBehaviors = new List<object>(_perCallBehaviors) { behavior };
        return new ExecutionContext<TResult>(_dispatcher, _serviceProvider, newBehaviors);
    }

    public async Task<ExecutionResult<TResult>> ExecuteAsync<TUseCaseParameter>(TUseCaseParameter useCaseParameter, CancellationToken cancellationToken = default)
        where TUseCaseParameter : IUseCaseParameter<TResult>
    {
        return await ExecuteInternalAsync(useCaseParameter, ExecutionScope.SingleUseCase, cancellationToken);
    }

    internal async Task<ExecutionResult<TResult>> ExecuteInternalAsync<TUseCaseParameter>(TUseCaseParameter useCaseParameter, IExecutionScope scope, CancellationToken cancellationToken = default)
        where TUseCaseParameter : IUseCaseParameter<TResult>
    {
        if (useCaseParameter == null)
        {
            throw new ArgumentNullException(nameof(useCaseParameter));
        }

        try
        {
            return await UseCasePipelineInvoker<TResult>.ExecuteAsync(
                _serviceProvider,
                useCaseParameter,
                _perCallBehaviors,
                scope,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Execution.Failure<TResult>($"Error executing use case: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Implementation of untyped execution context.
/// </summary>
internal class ExecutionContext : IExecutionContext
{
    private readonly IUseCaseDispatcher _dispatcher;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<object> _perCallBehaviors;

    public ExecutionContext(IUseCaseDispatcher dispatcher, IServiceProvider serviceProvider)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _perCallBehaviors = new List<object>();
    }

    internal ExecutionContext(IUseCaseDispatcher dispatcher, IServiceProvider serviceProvider, List<object> existingBehaviors)
    {
        _dispatcher = dispatcher;
        _serviceProvider = serviceProvider;
        _perCallBehaviors = new List<object>(existingBehaviors);
    }



    public IExecutionContext WithBehavior(Type behaviorType)
    {
        if (behaviorType == null)
        {
            throw new ArgumentNullException(nameof(behaviorType));
        }

        if (!behaviorType.IsGenericTypeDefinition)
        {
            throw new ArgumentException("Behavior type must be an open generic type definition (e.g., typeof(MyBehavior<,>))", nameof(behaviorType));
        }

        // Store the open generic type - we'll resolve it when we know the concrete parameter and result types
        return WithBehavior(new OpenGenericBehaviorDescriptor(behaviorType));
    }

    public IExecutionContext WithBehavior(object behavior)
    {
        if (behavior == null)
        {
            throw new ArgumentNullException(nameof(behavior));
        }

        var newBehaviors = new List<object>(_perCallBehaviors) { behavior };
        return new ExecutionContext(_dispatcher, _serviceProvider, newBehaviors);
    }

    public async Task<ExecutionResult<TResult>> ExecuteAsync<TResult>(IUseCaseParameter<TResult> useCaseParameter, CancellationToken cancellationToken = default)
        where TResult : notnull
    {
        var typedContext = new ExecutionContext<TResult>(_dispatcher, _serviceProvider, _perCallBehaviors);
        return await typedContext.ExecuteAsync(useCaseParameter, cancellationToken);
    }
}
