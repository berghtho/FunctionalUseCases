using Microsoft.Extensions.Logging;
using System.Globalization;

namespace FunctionalUseCases;

public static class Execution
{
    private static readonly ExecutionResult VoidSuccess = new();

    public static ExecutionResult<TResult> Success<TResult>(TResult value) where TResult : notnull => value;

    public static ExecutionResult Success() =>
        VoidSuccess;

    public static ExecutionResult<TResult> Failure<TResult>(IEnumerable<string> messages, int? errorCode = null, LogLevel logLevel = LogLevel.Error) where TResult : notnull =>
        Failure<TResult>(messages, ToErrorCode(errorCode), logLevel);

    public static ExecutionResult Failure(IEnumerable<string> messages, int? errorCode = null, LogLevel logLevel = LogLevel.Error) =>
        Failure(messages, ToErrorCode(errorCode), logLevel);

    public static ExecutionResult<TResult> Failure<TResult>(IEnumerable<string> messages, string? errorCode, LogLevel logLevel = LogLevel.Error, IDictionary<string, object?>? properties = null) where TResult : notnull =>
        new(new ExecutionError(messages) { ErrorCode = errorCode, LogLevel = logLevel, Properties = CopyProperties(properties) });

    public static ExecutionResult Failure(IEnumerable<string> messages, string? errorCode, LogLevel logLevel = LogLevel.Error, IDictionary<string, object?>? properties = null) =>
        new(new ExecutionError(messages) { ErrorCode = errorCode, LogLevel = logLevel, Properties = CopyProperties(properties) });

    public static ExecutionResult<TResult> Failure<TResult>(Exception exception, LogLevel logLevel = LogLevel.Error, bool suppressPipelineLogging = false) where TResult : notnull =>
        new(new ExecutionError(GetExceptionMessages(exception)) { Exception = exception, LogLevel = logLevel });

    public static ExecutionResult<TResult> Failure<TResult>(string message, Exception ex, LogLevel logLevel = LogLevel.Error) where TResult : notnull =>
        new(new ExecutionError(new[] { message }.Concat(GetExceptionMessages(ex))) { Exception = ex, LogLevel = logLevel });

    public static ExecutionResult<TResult> Failure<TResult>(string message, int? errorCode = null, LogLevel logLevel = LogLevel.Error) where TResult : notnull =>
        Failure<TResult>(new[] { message }, errorCode, logLevel);

    public static ExecutionResult<TResult> Failure<TResult>(string message, string errorCode, LogLevel logLevel = LogLevel.Error, IDictionary<string, object?>? properties = null) where TResult : notnull =>
        Failure<TResult>(new[] { message }, errorCode, logLevel, properties);

    public static ExecutionResult<TResult> Failure<TResult>(ExecutionResult result, string? errorCode = null,
        LogLevel logLevel = LogLevel.Error) where TResult : notnull =>
        Failure<TResult>(result.CheckedError, errorCode, logLevel);

    public static ExecutionResult Failure(ExecutionResult result, string? errorCode = null, LogLevel logLevel = LogLevel.Error) =>
        Failure(result.CheckedError, errorCode, logLevel);

    public static ExecutionResult Failure(string message, int? errorCode = null, LogLevel logLevel = LogLevel.Error) =>
        Failure(new[] { message }, errorCode, logLevel);

    public static ExecutionResult Failure(string message, string errorCode, LogLevel logLevel = LogLevel.Error, IDictionary<string, object?>? properties = null) =>
        Failure(new[] { message }, errorCode, logLevel, properties);

    public static ExecutionResult Failure(string message, Exception ex, int? errorCode = null, LogLevel logLevel = LogLevel.Error) =>
        new(new ExecutionError(new[] { message }.Concat(GetExceptionMessages(ex)))
        {
            ErrorCode = ToErrorCode(errorCode),
            Exception = ex,
            LogLevel = logLevel
        });

    public static ExecutionResult Failure(Exception ex, int? errorCode = null, LogLevel logLevel = LogLevel.Error) =>
        new(new ExecutionError(GetExceptionMessages(ex))
        {
            ErrorCode = ToErrorCode(errorCode),
            Exception = ex,
            LogLevel = logLevel
        });

    public static ExecutionResult Combine<T>(params T[] results)
        where T : ExecutionResult =>
        results.All(x => x.ExecutionSucceeded)
            ? Success()
            : new ExecutionResult(ConcatError(results));

    private static ExecutionResult<T> Failure<T>(ExecutionError error, string? errorCode, LogLevel logLevel) where T : notnull =>
        new(CopyError(error, errorCode, logLevel));

    private static ExecutionResult Failure(ExecutionError error, string? errorCode, LogLevel logLevel) =>
        new(CopyError(error, errorCode, logLevel));

    private static ExecutionError ConcatError<T>(params T[] results)
        where T : ExecutionResult
    {
        var errors = results.Select(x => x.Error).Where(x => x is not null).Cast<ExecutionError>().ToArray();
        var properties = errors
            .SelectMany(x => x.Properties)
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.Ordinal);

        return new ExecutionError(errors.SelectMany(x => x.Messages))
        {
            ErrorCode = errors.Select(x => x.ErrorCode).FirstOrDefault(x => x is not null),
            Exception = errors.Select(x => x.Exception).FirstOrDefault(x => x is not null),
            LogLevel = errors.Select(x => x.LogLevel).DefaultIfEmpty(LogLevel.Error).Max(),
            Logged = errors.All(x => x.Logged),
            Properties = properties
        };
    }

    private static ExecutionError CopyError(ExecutionError error, string? errorCode, LogLevel logLevel) =>
        new(error.Messages)
        {
            ErrorCode = errorCode ?? error.ErrorCode,
            Exception = error.Exception,
            LogLevel = logLevel,
            Logged = error.Logged,
            Properties = CopyProperties(error.Properties)
        };

    private static Dictionary<string, object?> CopyProperties(IDictionary<string, object?>? properties) =>
        properties is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(properties, StringComparer.Ordinal);

    private static string? ToErrorCode(int? errorCode) =>
        errorCode?.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<string> GetExceptionMessages(Exception ex)
    {
        if (ex is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                foreach (var innerMessage in GetExceptionMessages(innerException))
                {
                    yield return innerMessage;
                }
            }
        }
        else
        {
            yield return ex.Message;
        }

        if (ex.InnerException is null)
        {
            yield break;
        }

        foreach (var innerMessage in GetExceptionMessages(ex.InnerException))
        {
            yield return innerMessage;
        }
    }
}
