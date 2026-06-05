using FunctionalUseCases.AspNetCore;
using Microsoft.AspNetCore.Mvc;

namespace FunctionalUseCases.Tests;

public class AspNetCoreExecutionResultExtensionsTests
{
    [Fact]
    public void ToActionResult_WithSuccess_ShouldReturnOkObjectResult()
    {
        var result = Execution.Success("value");

        var actionResult = result.ToActionResult();

        var okResult = actionResult.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe("value");
    }

    [Fact]
    public void ToActionResult_WithFailure_ShouldReturnProblemDetails()
    {
        var result = Execution.Failure<string>(
            "Customer missing",
            "CUSTOMER_NOT_FOUND",
            properties: new Dictionary<string, object?>
            {
                ["statusCode"] = 404,
                ["customerId"] = 42
            });

        var actionResult = result.ToActionResult();

        var objectResult = actionResult.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(404);
        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Detail.ShouldBe("Customer missing");
        problemDetails.Extensions["errorCode"].ShouldBe("CUSTOMER_NOT_FOUND");
        problemDetails.Extensions["customerId"].ShouldBe(42);
    }

    [Fact]
    public void ToProblemDetails_ShouldHideExceptionByDefault()
    {
        var error = Execution.Failure("Failed", new InvalidOperationException("Sensitive")).CheckedError;

        var problemDetails = error.ToProblemDetails();

        problemDetails.Extensions.ShouldNotContainKey("exception");
    }

    [Fact]
    public void ToProblemDetails_WithOption_ShouldIncludeExceptionDetails()
    {
        var error = Execution.Failure("Failed", new InvalidOperationException("Sensitive")).CheckedError;

        var problemDetails = error.ToProblemDetails(new ExecutionResultHttpOptions
        {
            IncludeExceptionDetails = true
        });

        problemDetails.Extensions["exceptionType"].ShouldBe(typeof(InvalidOperationException).FullName);
        problemDetails.Extensions["exception"].ShouldBeOfType<string>().ShouldContain("Sensitive");
    }
}
