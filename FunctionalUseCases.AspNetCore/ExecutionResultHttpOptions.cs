using Microsoft.AspNetCore.Http;

namespace FunctionalUseCases.AspNetCore;

public sealed class ExecutionResultHttpOptions
{
    public Func<ExecutionError, int> StatusCodeSelector { get; init; } = DefaultStatusCodeSelector;

    public bool IncludeExceptionDetails { get; init; }

    private static int DefaultStatusCodeSelector(ExecutionError error)
    {
        if (error.Properties.TryGetValue("statusCode", out var statusCode) &&
            statusCode is int propertyStatusCode)
        {
            return propertyStatusCode;
        }

        return int.TryParse(error.ErrorCode, out var errorCode) &&
               errorCode is >= 400 and <= 599
            ? errorCode
            : StatusCodes.Status500InternalServerError;
    }
}
