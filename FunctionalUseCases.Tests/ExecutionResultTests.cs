namespace FunctionalUseCases.Tests;

public class ExecutionResultTests
{
    [Fact]
    public void ExecutionResult_Success_ShouldReturnSuccessfulResult()
    {
        // Act
        var result = Execution.Success();

        // Assert
        result.ExecutionSucceeded.ShouldBeTrue();
        result.ExecutionFailed.ShouldBeFalse();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void ExecutionResult_Failure_ShouldReturnFailedResult()
    {
        // Arrange
        const string errorMessage = "Test error";

        // Act
        var result = Execution.Failure(errorMessage);

        // Assert
        result.ExecutionSucceeded.ShouldBeFalse();
        result.ExecutionFailed.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldBe(errorMessage);
    }

    [Fact]
    public void ExecutionResult_ThrowIfFailed_ShouldThrowWhenFailed()
    {
        // Arrange
        var originalException = new InvalidOperationException("Original");
        var result = Execution.Failure("Test error", originalException);

        // Act & Assert
        var exception = Should.Throw<ExecutionException>(() => result.ThrowIfFailed());
        exception.Message.ShouldContain("Test error");
        exception.InnerException.ShouldBeSameAs(originalException);
    }

    [Fact]
    public void ExecutionResult_ThrowIfFailed_ShouldNotThrowWhenSuccessful()
    {
        // Arrange
        var result = Execution.Success();

        // Act & Assert (no exception should be thrown)
        result.ThrowIfFailed();
    }
}
