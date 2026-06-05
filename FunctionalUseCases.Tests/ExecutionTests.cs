using Microsoft.Extensions.Logging;

namespace FunctionalUseCases.Tests;

public class ExecutionTests
{
    [Fact]
    public void Execution_Success_ShouldReturnSuccessfulResult()
    {
        // Act
        var result = Execution.Success();

        // Assert
        result.ExecutionSucceeded.ShouldBeTrue();
        result.ExecutionFailed.ShouldBeFalse();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Execution_Success_WithValue_ShouldReturnSuccessfulResultWithValue()
    {
        // Arrange
        const string value = "test value";

        // Act
        var result = Execution.Success(value);

        // Assert
        result.ExecutionSucceeded.ShouldBeTrue();
        result.ExecutionFailed.ShouldBeFalse();
        result.Error.ShouldBeNull();
        result.CheckedValue.ShouldBe(value);
    }

    [Fact]
    public void Execution_Failure_WithSingleMessage_ShouldReturnFailedResult()
    {
        // Arrange
        const string message = "Test error";

        // Act
        var result = Execution.Failure(message);

        // Assert
        result.ExecutionSucceeded.ShouldBeFalse();
        result.ExecutionFailed.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldBe(message);
    }

    [Fact]
    public void Execution_Failure_WithMultipleMessages_ShouldReturnFailedResult()
    {
        // Arrange
        var messages = new[] { "Error 1", "Error 2" };

        // Act
        var result = Execution.Failure(messages);

        // Assert
        result.ExecutionSucceeded.ShouldBeFalse();
        result.ExecutionFailed.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldBe("Error 1; Error 2");
    }

    [Fact]
    public void Execution_Failure_WithErrorCodeAndLogLevel_ShouldSetProperties()
    {
        // Arrange
        const string message = "Test error";
        const int errorCode = 404;
        const LogLevel logLevel = LogLevel.Warning;

        // Act
        var result = Execution.Failure(message, errorCode, logLevel);

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.ErrorCode.ShouldBe("404");
        result.Error.LogLevel.ShouldBe(logLevel);
    }

    [Fact]
    public void Execution_Failure_WithException_ShouldExtractMessage()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act
        var result = Execution.Failure<string>(exception);

        // Assert
        result.ExecutionSucceeded.ShouldBeFalse();
        result.ExecutionFailed.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldContain("Test exception");
        result.Error.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public void Execution_Failure_WithMessageAndException_ShouldCombineMessages()
    {
        // Arrange
        const string message = "Custom error";
        var exception = new InvalidOperationException("Test exception");

        // Act
        var result = Execution.Failure<string>(message, exception);

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldContain("Custom error");
        result.Error.Message.ShouldContain("Test exception");
        result.Error.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public void Execution_Failure_WithStringCodeAndProperties_ShouldSetStructuredMetadata()
    {
        // Arrange
        var properties = new Dictionary<string, object?> { ["customerId"] = 42 };

        // Act
        var result = Execution.Failure<string>(
            "Customer missing",
            "CUSTOMER_NOT_FOUND",
            properties: properties);

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.ErrorCode.ShouldBe("CUSTOMER_NOT_FOUND");
        result.Error.Properties["customerId"].ShouldBe(42);
    }

    [Fact]
    public void Execution_Failure_WithAggregateException_ShouldExtractAllMessages()
    {
        // Arrange
        var innerExceptions = new Exception[]
        {
            new InvalidOperationException("Error 1"),
            new ArgumentException("Error 2")
        };
        var aggregateException = new AggregateException(innerExceptions);

        // Act
        var result = Execution.Failure<string>(aggregateException);

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldContain("Error 1");
        result.Error.Message.ShouldContain("Error 2");
    }

    [Fact]
    public void Execution_Combine_WithAllSuccessful_ShouldReturnSuccess()
    {
        // Arrange
        var result1 = Execution.Success();
        var result2 = Execution.Success();
        var result3 = Execution.Success();

        // Act
        var combined = Execution.Combine(result1, result2, result3);

        // Assert
        combined.ExecutionSucceeded.ShouldBeTrue();
        combined.Error.ShouldBeNull();
    }

    [Fact]
    public void Execution_Combine_WithAnyFailed_ShouldReturnFailure()
    {
        // Arrange
        var successResult = Execution.Success();
        var failureResult = Execution.Failure("Test error");

        // Act
        var combined = Execution.Combine(successResult, failureResult);

        // Assert
        combined.ExecutionSucceeded.ShouldBeFalse();
        combined.ExecutionFailed.ShouldBeTrue();
        combined.Error.ShouldNotBeNull();
        combined.Error.Message.ShouldContain("Test error");
    }

    [Fact]
    public void Execution_Combine_WithMultipleFailures_ShouldCombineMessages()
    {
        // Arrange
        var failure1 = Execution.Failure("Error 1");
        var failure2 = Execution.Failure("Error 2");

        // Act
        var combined = Execution.Combine(failure1, failure2);

        // Assert
        combined.ExecutionSucceeded.ShouldBeFalse();
        combined.Error.ShouldNotBeNull();
        combined.Error.Message.ShouldContain("Error 1");
        combined.Error.Message.ShouldContain("Error 2");
    }
}
