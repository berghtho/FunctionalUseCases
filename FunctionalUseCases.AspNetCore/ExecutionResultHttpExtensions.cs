using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FunctionalUseCases.AspNetCore;

public static class ExecutionResultHttpExtensions
{
    public static IActionResult ToActionResult<T>(
        this ExecutionResult<T> result,
        ExecutionResultHttpOptions? options = null)
        where T : notnull =>
        result.ExecutionSucceeded
            ? new OkObjectResult(result.CheckedValue)
            : CreateErrorResult(result.Error, options);

    public static IActionResult ToActionResult(
        this ExecutionResult result,
        ExecutionResultHttpOptions? options = null) =>
        result.ExecutionSucceeded
            ? new NoContentResult()
            : CreateErrorResult(result.Error, options);

    public static ProblemDetails ToProblemDetails(
        this ExecutionError error,
        ExecutionResultHttpOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        options ??= new ExecutionResultHttpOptions();
        var statusCode = options.StatusCodeSelector(error);
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = "Use case execution failed",
            Detail = error.Message
        };

        if (error.ErrorCode is not null)
        {
            problemDetails.Extensions["errorCode"] = error.ErrorCode;
        }

        foreach (var property in error.Properties)
        {
            problemDetails.Extensions[property.Key] = property.Value;
        }

        if (options.IncludeExceptionDetails && error.Exception is not null)
        {
            problemDetails.Extensions["exceptionType"] = error.Exception.GetType().FullName;
            problemDetails.Extensions["exception"] = error.Exception.ToString();
        }

        return problemDetails;
    }

    private static ObjectResult CreateErrorResult(
        ExecutionError? error,
        ExecutionResultHttpOptions? options)
    {
        error ??= new ExecutionError("Unknown Error");
        var problemDetails = error.ToProblemDetails(options);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError
        };
    }
}
