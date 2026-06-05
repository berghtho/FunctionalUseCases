namespace FunctionalUseCases;

/// <summary>
/// Mediator-style UseCase dispatcher that resolves handlers via dependency injection.
/// </summary>
public class UseCaseDispatcher(IServiceProvider serviceProvider) : IUseCaseDispatcher
{
    /// <summary>
    /// Gets the service provider used for dependency injection.
    /// </summary>
    public IServiceProvider ServiceProvider { get; } = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Executes a use case by resolving the appropriate handler and running it through the pipeline behaviors.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the use case.</typeparam>
    /// <param name="useCaseParameter">The use case parameter to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An ExecutionResult containing the result or error information.</returns>
    public async Task<ExecutionResult<TResult>> ExecuteAsync<TResult>(IUseCaseParameter<TResult> useCaseParameter, CancellationToken cancellationToken = default)
        where TResult : notnull
    {
        if (useCaseParameter == null)
        {
            throw new ArgumentNullException(nameof(useCaseParameter));
        }

        try
        {
            return await UseCasePipelineInvoker<TResult>.ExecuteAsync(
                ServiceProvider,
                useCaseParameter,
                perCallBehaviors: null,
                ExecutionScope.SingleUseCase,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Execution.Failure<TResult>($"Error executing use case: {ex.Message}", ex);
        }
    }
}
