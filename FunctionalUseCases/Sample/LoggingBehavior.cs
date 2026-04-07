using Microsoft.Extensions.Logging;

namespace FunctionalUseCases.Sample;

/// <summary>
/// Sample execution behavior that logs use case execution.
/// Demonstrates how to implement cross-cutting concerns like logging, timing, validation, etc.
/// </summary>
/// <typeparam name="TUseCaseParameter">The type of use case parameter being handled.</typeparam>
/// <typeparam name="TResult">The type of result returned by the use case.</typeparam>
public class LoggingBehavior<TUseCaseParameter, TResult>(ILogger<LoggingBehavior<TUseCaseParameter, TResult>> logger)
    : IExecutionBehavior<TUseCaseParameter, TResult>
    where TUseCaseParameter : IUseCaseParameter<TResult>
    where TResult : notnull
{
    private ILogger<LoggingBehavior<TUseCaseParameter, TResult>> Logger { get; } =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<ExecutionResult<TResult>> ExecuteAsync(TUseCaseParameter useCaseParameter, PipelineBehaviorDelegate<TResult> next, CancellationToken cancellationToken = default)
    {
        var useCaseParameterName = typeof(TUseCaseParameter).Name;
        var resultTypeName = typeof(TResult).Name;

        Logger.LogInformation("Starting execution of use case: {UseCaseParameterName} -> {ResultType}", useCaseParameterName, resultTypeName);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = await next().ConfigureAwait(false);

            stopwatch.Stop();

            if (result.ExecutionSucceeded)
            {
                Logger.LogInformation("Successfully executed use case: {UseCaseParameterName} -> {ResultType} in {ElapsedMilliseconds}ms",
                    useCaseParameterName, resultTypeName, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                Logger.LogWarning("Use case execution failed: {UseCaseParameterName} -> {ResultType} in {ElapsedMilliseconds}ms. Error: {ErrorMessage}",
                    useCaseParameterName, resultTypeName, stopwatch.ElapsedMilliseconds, result.Error?.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Exception occurred during use case execution: {UseCaseParameterName} -> {ResultType} in {ElapsedMilliseconds}ms",
                useCaseParameterName, resultTypeName, stopwatch.ElapsedMilliseconds);

            return Execution.Failure<TResult>($"Exception in LoggingBehavior: {ex.Message}", ex);
        }
    }
}
