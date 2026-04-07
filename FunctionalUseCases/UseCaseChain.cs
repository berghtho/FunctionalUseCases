using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace FunctionalUseCases;

/// <summary>
/// Represents a chain of use cases that can be executed sequentially.
/// Execution stops on the first failure, and error handling can be provided at the end of the chain.
/// Results are passed between use cases in the chain.
/// </summary>
/// <typeparam name="TResult">The final result type of the chain.</typeparam>
public class UseCaseChain<TResult>
    where TResult : notnull
{
    private readonly IUseCaseDispatcher _dispatcher;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITransactionManager? _transactionManager;
    private readonly ILogger? _logger;
    internal readonly List<object> _perCallBehaviors;
    internal readonly List<Func<IExecutionScope, object?, CancellationToken, Task<(bool Success, object? Result, ExecutionError? Error)>>> _steps;
    private Func<ExecutionError, CancellationToken, Task<ExecutionResult<TResult>>>? _errorHandler;
    private readonly string _chainId;

    internal UseCaseChain(IUseCaseDispatcher dispatcher, IServiceProvider serviceProvider, ITransactionManager? transactionManager = null, ILogger? logger = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _transactionManager = transactionManager;
        _logger = logger;
        _perCallBehaviors = new List<object>();
        _steps = new List<Func<IExecutionScope, object?, CancellationToken, Task<(bool Success, object? Result, ExecutionError? Error)>>>();
        _chainId = Guid.NewGuid().ToString();
    }

    internal UseCaseChain(IUseCaseDispatcher dispatcher, IServiceProvider serviceProvider, ITransactionManager? transactionManager, ILogger? logger, List<object> perCallBehaviors, string chainId)
    {
        _dispatcher = dispatcher;
        _serviceProvider = serviceProvider;
        _transactionManager = transactionManager;
        _logger = logger;
        _perCallBehaviors = new List<object>(perCallBehaviors);
        _steps = new List<Func<IExecutionScope, object?, CancellationToken, Task<(bool Success, object? Result, ExecutionError? Error)>>>();
        _chainId = chainId;
    }



    /// <summary>
    /// Adds a behavior to this use case chain using an open generic type definition.
    /// The behavior will be applied to all use cases in the chain.
    /// </summary>
    /// <param name="behaviorType">The open generic type definition of the behavior (e.g., typeof(TransactionBehavior&lt;,&gt;)).</param>
    /// <returns>A new chain with the behavior added.</returns>
    public UseCaseChain<TResult> WithBehavior(Type behaviorType)
    {
        if (behaviorType == null)
        {
            throw new ArgumentNullException(nameof(behaviorType));
        }

        if (!behaviorType.IsGenericTypeDefinition)
        {
            throw new ArgumentException("Behavior type must be an open generic type definition (e.g., typeof(MyBehavior<,>))", nameof(behaviorType));
        }

        return WithBehavior(new OpenGenericBehaviorDescriptor(behaviorType));
    }

    /// <summary>
    /// Adds a behavior instance to this use case chain.
    /// The behavior will be applied to all use cases in the chain.
    /// </summary>
    /// <param name="behavior">The behavior instance to add.</param>
    /// <returns>A new chain with the behavior added.</returns>
    public UseCaseChain<TResult> WithBehavior(object behavior)
    {
        if (behavior == null)
        {
            throw new ArgumentNullException(nameof(behavior));
        }

        var newBehaviors = new List<object>(_perCallBehaviors) { behavior };
        var newChain = new UseCaseChain<TResult>(_dispatcher, _serviceProvider, _transactionManager, _logger, newBehaviors, _chainId);

        // Copy existing steps
        newChain._steps.AddRange(_steps);

        return newChain;
    }

    /// <summary>
    /// Adds a use case to the chain.
    /// </summary>
    /// <typeparam name="TNextResult">The result type of the next use case.</typeparam>
    /// <param name="useCaseParameter">The use case parameter to execute.</param>
    /// <returns>A new chain with the added use case.</returns>
    public UseCaseChain<TNextResult> Then<TNextResult>(IUseCaseParameter<TNextResult> useCaseParameter)
        where TNextResult : notnull
    {
        if (useCaseParameter == null)
        {
            throw new ArgumentNullException(nameof(useCaseParameter));
        }

        var newChain = new UseCaseChain<TNextResult>(_dispatcher, _serviceProvider, _transactionManager, _logger, _perCallBehaviors, _chainId);

        // Copy existing steps
        newChain._steps.AddRange(_steps);

        // Add the new step
        newChain._steps.Add(async (scope, previousResult, cancellationToken) =>
        {
            // Create execution context with per-call behaviors
            var context = new ExecutionContext<TNextResult>(_dispatcher, _serviceProvider);
            foreach (var behavior in newChain._perCallBehaviors)
            {
                context = (ExecutionContext<TNextResult>)context.WithBehavior(behavior);
            }

            var result = await context.ExecuteInternalAsync(useCaseParameter, scope, cancellationToken);
            return result.ExecutionSucceeded
                ? (true, (object?)result.CheckedValue, (ExecutionError?)null)
                : (false, (object?)null, result.CheckedError);
        });

        return newChain;
    }

    /// <summary>
    /// Adds a use case to the chain using a function that receives the previous result.
    /// </summary>
    /// <typeparam name="TNextResult">The result type of the next use case.</typeparam>
    /// <param name="useCaseParameterFactory">Function that takes the previous result and returns the next use case parameter.</param>
    /// <returns>A new chain with the added use case.</returns>
    public UseCaseChain<TNextResult> Then<TNextResult>(Func<TResult, IUseCaseParameter<TNextResult>> useCaseParameterFactory)
        where TNextResult : notnull
    {
        if (useCaseParameterFactory == null)
        {
            throw new ArgumentNullException(nameof(useCaseParameterFactory));
        }

        var newChain = new UseCaseChain<TNextResult>(_dispatcher, _serviceProvider, _transactionManager, _logger, _perCallBehaviors, _chainId);

        // Copy existing steps
        newChain._steps.AddRange(_steps);

        // Add the new step
        newChain._steps.Add(async (scope, previousResult, cancellationToken) =>
        {
            if (previousResult is not TResult typedPreviousResult)
            {
                return (false, null, new ExecutionError("Previous result type mismatch in chain"));
            }

            var useCaseParameter = useCaseParameterFactory(typedPreviousResult);

            // Create execution context with per-call behaviors
            var context = new ExecutionContext<TNextResult>(_dispatcher, _serviceProvider);
            foreach (var behavior in newChain._perCallBehaviors)
            {
                context = (ExecutionContext<TNextResult>)context.WithBehavior(behavior);
            }

            var result = await context.ExecuteInternalAsync(useCaseParameter, scope, cancellationToken);

            return result.ExecutionSucceeded
                ? (true, (object?)result.CheckedValue, (ExecutionError?)null)
                : (false, (object?)null, result.CheckedError);
        });

        return newChain;
    }

    /// <summary>
    /// Specifies an error handler for the chain.
    /// The error handler will be called if any use case in the chain fails.
    /// </summary>
    /// <param name="errorHandler">The error handler function.</param>
    /// <returns>The same chain with error handling configured.</returns>
    public UseCaseChain<TResult> OnError(Func<ExecutionError, Task<ExecutionResult<TResult>>> errorHandler)
    {
        if (errorHandler == null)
        {
            throw new ArgumentNullException(nameof(errorHandler));
        }

        _errorHandler = (error, cancellationToken) => errorHandler(error);
        return this;
    }

    /// <summary>
    /// Specifies an error handler for the chain with cancellation token support.
    /// The error handler will be called if any use case in the chain fails.
    /// </summary>
    /// <param name="errorHandler">The error handler function.</param>
    /// <returns>The same chain with error handling configured.</returns>
    public UseCaseChain<TResult> OnError(Func<ExecutionError, CancellationToken, Task<ExecutionResult<TResult>>> errorHandler)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        return this;
    }

    /// <summary>
    /// Executes the entire chain of use cases sequentially.
    /// Execution stops on the first failure unless an error handler is provided.
    /// Results are passed between use cases in the chain.
    /// If a transaction manager is provided, the entire chain executes within a single transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final execution result.</returns>
    public async Task<ExecutionResult<TResult>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (_steps.Count == 0)
        {
            return Execution.Failure<TResult>("Chain is empty. Add at least one use case using Then().");
        }

        ITransaction? transaction = null;
        object? currentResult = null;

        try
        {
            // Begin transaction if transaction manager is available
            if (_transactionManager != null)
            {
                transaction = await _transactionManager.BeginTransactionAsync(cancellationToken);
                _logger?.LogDebug("Transaction started for use case chain");
            }

            // Execute each step in the chain, passing results between steps
            for (int i = 0; i < _steps.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var step = _steps[i];
                var scope = ExecutionScope.Chain(_chainId, i == 0, i == _steps.Count - 1);
                var (success, result, error) = await step(scope, currentResult, cancellationToken);

                if (!success)
                {
                    // Rollback transaction on failure
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _logger?.LogDebug("Transaction rolled back due to use case chain failure");
                    }

                    // If we have an error handler, use it
                    if (_errorHandler != null && error != null)
                    {
                        return await _errorHandler(error, cancellationToken);
                    }

                    // Otherwise, return the failure result
                    return Execution.Failure<TResult>(error?.Message ?? "Unknown error in chain execution",
                        error?.ErrorCode ?? 0, error?.LogLevel ?? LogLevel.Error);
                }

                currentResult = result;
            }

            // Commit transaction on success
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
                _logger?.LogDebug("Transaction committed for use case chain");
            }

            // All steps succeeded, return the final result
            if (currentResult is TResult finalResult)
            {
                return Execution.Success(finalResult);
            }

            // This shouldn't happen if the chain is constructed correctly
            return Execution.Failure<TResult>("Chain execution completed but final result type mismatch.");
        }
        catch (OperationCanceledException)
        {
            // Rollback transaction on cancellation
            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    _logger?.LogDebug("Transaction rolled back due to cancellation");
                }
                catch (Exception rollbackEx)
                {
                    _logger?.LogError(rollbackEx, "Failed to rollback transaction during cancellation");
                }
            }

            return Execution.Failure<TResult>("Chain execution was cancelled.");
        }
        catch (Exception ex)
        {
            // Rollback transaction on exception
            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger?.LogDebug("Transaction rolled back due to exception");
                }
                catch (Exception rollbackEx)
                {
                    _logger?.LogError(rollbackEx, "Failed to rollback transaction during exception handling");
                }
            }

            // If we have an error handler, use it for exceptions too
            if (_errorHandler != null)
            {
                var error = new ExecutionError($"Exception during chain execution: {ex.Message}");
                return await _errorHandler(error, cancellationToken);
            }

            return Execution.Failure<TResult>($"Exception during chain execution: {ex.Message}", ex);
        }
        finally
        {
            // Ensure transaction is disposed
            transaction?.Dispose();
        }
    }

}

/// <summary>
/// Non-generic version of UseCaseChain for chains that don't return a specific value.
/// </summary>
public class UseCaseChain
{
    private readonly IUseCaseDispatcher _dispatcher;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITransactionManager? _transactionManager;
    private readonly ILogger? _logger;
    internal readonly List<object> _perCallBehaviors;
    private readonly List<Func<IExecutionScope, object?, CancellationToken, Task<(bool Success, object? Result, ExecutionError? Error)>>> _steps;
    private Func<ExecutionError, CancellationToken, Task<ExecutionResult>>? _errorHandler;
    private readonly string _chainId;

    internal UseCaseChain(IUseCaseDispatcher dispatcher, IServiceProvider serviceProvider, ITransactionManager? transactionManager = null, ILogger? logger = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _transactionManager = transactionManager;
        _logger = logger;
        _perCallBehaviors = new List<object>();
        _steps = new List<Func<IExecutionScope, object?, CancellationToken, Task<(bool Success, object? Result, ExecutionError? Error)>>>();
        _chainId = Guid.NewGuid().ToString();
    }

    internal UseCaseChain(IUseCaseDispatcher dispatcher, IServiceProvider serviceProvider, ITransactionManager? transactionManager, ILogger? logger, List<object> perCallBehaviors, string chainId)
    {
        _dispatcher = dispatcher;
        _serviceProvider = serviceProvider;
        _transactionManager = transactionManager;
        _logger = logger;
        _perCallBehaviors = new List<object>(perCallBehaviors);
        _steps = new List<Func<IExecutionScope, object?, CancellationToken, Task<(bool Success, object? Result, ExecutionError? Error)>>>();
        _chainId = chainId;
    }



    /// <summary>
    /// Adds a behavior to this use case chain using an open generic type definition.
    /// The behavior will be applied to all use cases in the chain.
    /// </summary>
    /// <param name="behaviorType">The open generic type definition of the behavior (e.g., typeof(TransactionBehavior&lt;,&gt;)).</param>
    /// <returns>A new chain with the behavior added.</returns>
    public UseCaseChain WithBehavior(Type behaviorType)
    {
        if (behaviorType == null)
        {
            throw new ArgumentNullException(nameof(behaviorType));
        }

        if (!behaviorType.IsGenericTypeDefinition)
        {
            throw new ArgumentException("Behavior type must be an open generic type definition (e.g., typeof(MyBehavior<,>))", nameof(behaviorType));
        }

        return WithBehavior(new OpenGenericBehaviorDescriptor(behaviorType));
    }

    /// <summary>
    /// Adds a behavior instance to this use case chain.
    /// The behavior will be applied to all use cases in the chain.
    /// </summary>
    /// <param name="behavior">The behavior instance to add.</param>
    /// <returns>A new chain with the behavior added.</returns>
    public UseCaseChain WithBehavior(object behavior)
    {
        if (behavior == null)
        {
            throw new ArgumentNullException(nameof(behavior));
        }

        var newBehaviors = new List<object>(_perCallBehaviors) { behavior };
        var newChain = new UseCaseChain(_dispatcher, _serviceProvider, _transactionManager, _logger, newBehaviors, _chainId);

        // Copy existing steps
        newChain._steps.AddRange(_steps);

        return newChain;
    }

    /// <summary>
    /// Adds a use case to the chain.
    /// </summary>
    /// <typeparam name="TResult">The result type of the use case.</typeparam>
    /// <param name="useCaseParameter">The use case parameter to execute.</param>
    /// <returns>A new typed chain with the added use case.</returns>
    public UseCaseChain<TResult> Then<TResult>(IUseCaseParameter<TResult> useCaseParameter)
        where TResult : notnull
    {
        if (useCaseParameter == null)
        {
            throw new ArgumentNullException(nameof(useCaseParameter));
        }

        var newChain = new UseCaseChain<TResult>(_dispatcher, _serviceProvider, _transactionManager, _logger, _perCallBehaviors, _chainId);

        // Copy existing steps
        newChain._steps.AddRange(_steps);

        // Add the new step
        newChain._steps.Add(async (scope, previousResult, cancellationToken) =>
        {
            // Create execution context with per-call behaviors
            var context = new ExecutionContext<TResult>(_dispatcher, _serviceProvider);
            foreach (var behavior in newChain._perCallBehaviors)
            {
                context = (ExecutionContext<TResult>)context.WithBehavior(behavior);
            }

            var result = await context.ExecuteInternalAsync(useCaseParameter, scope, cancellationToken);
            return result.ExecutionSucceeded
                ? (true, (object?)result.CheckedValue, (ExecutionError?)null)
                : (false, (object?)null, result.CheckedError);
        });

        return newChain;
    }

    /// <summary>
    /// Specifies an error handler for the chain.
    /// The error handler will be called if any use case in the chain fails.
    /// </summary>
    /// <param name="errorHandler">The error handler function.</param>
    /// <returns>The same chain with error handling configured.</returns>
    public UseCaseChain OnError(Func<ExecutionError, Task<ExecutionResult>> errorHandler)
    {
        if (errorHandler == null)
        {
            throw new ArgumentNullException(nameof(errorHandler));
        }

        _errorHandler = (error, cancellationToken) => errorHandler(error);
        return this;
    }

    /// <summary>
    /// Specifies an error handler for the chain with cancellation token support.
    /// The error handler will be called if any use case in the chain fails.
    /// </summary>
    /// <param name="errorHandler">The error handler function.</param>
    /// <returns>The same chain with error handling configured.</returns>
    public UseCaseChain OnError(Func<ExecutionError, CancellationToken, Task<ExecutionResult>> errorHandler)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        return this;
    }

    /// <summary>
    /// Executes the entire chain of use cases sequentially.
    /// Execution stops on the first failure unless an error handler is provided.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final execution result.</returns>
    public async Task<ExecutionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (_steps.Count == 0)
        {
            return Execution.Failure("Chain is empty. Add at least one use case using Then().");
        }

        object? currentResult = null;

        try
        {
            // Execute each step in the chain, passing results between steps
            for (var i = 0; i < _steps.Count; i++)
            {
                var scope = ExecutionScope.Chain(_chainId, i == 0, i == _steps.Count - 1);
                cancellationToken.ThrowIfCancellationRequested();

                var (success, result, error) = await _steps[i](scope, currentResult, cancellationToken);

                if (!success)
                {
                    // If we have an error handler, use it
                    if (_errorHandler != null && error != null)
                    {
                        return await _errorHandler(error, cancellationToken);
                    }

                    // Otherwise, return the failure result
                    return Execution.Failure(error?.Message ?? "Unknown error in chain execution");
                }

                currentResult = result;
            }

            // All steps succeeded
            return Execution.Success();
        }
        catch (OperationCanceledException)
        {
            return Execution.Failure("Chain execution was cancelled.");
        }
        catch (Exception ex)
        {
            // If we have an error handler, use it for exceptions too
            if (_errorHandler != null)
            {
                var error = new ExecutionError($"Exception during chain execution: {ex.Message}");
                return await _errorHandler(error, cancellationToken);
            }

            return Execution.Failure($"Exception during chain execution: {ex.Message}", ex);
        }
    }
}
