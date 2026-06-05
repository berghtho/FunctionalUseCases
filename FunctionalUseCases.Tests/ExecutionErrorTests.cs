using Microsoft.Extensions.Logging;

namespace FunctionalUseCases.Tests;

public class ExecutionErrorTests
{
    [Fact]
    public void ExecutionError_Constructor_WithSingleMessage_ShouldSetMessage()
    {
        // Arrange
        const string message = "Test error";

        // Act
        var error = new ExecutionError(message);

        // Assert
        error.Message.ShouldBe(message);
        error.Messages.ShouldHaveSingleItem();
        error.Messages[0].ShouldBe(message);
    }

    [Fact]
    public void ExecutionError_Constructor_WithMultipleMessages_ShouldJoinMessages()
    {
        // Arrange
        var messages = new[] { "Error 1", "Error 2", "Error 3" };

        // Act
        var error = new ExecutionError(messages);

        // Assert
        error.Message.ShouldBe("Error 1; Error 2; Error 3");
        error.Messages.Count.ShouldBe(3);
        error.Messages.ShouldBe(messages);
    }

    [Fact]
    public void ExecutionError_Constructor_WithEnumerable_ShouldSetMessages()
    {
        // Arrange
        var messages = new List<string> { "Error 1", "Error 2" };

        // Act
        var error = new ExecutionError(messages);

        // Assert
        error.Messages.Count.ShouldBe(2);
        error.Message.ShouldBe("Error 1; Error 2");
    }

    [Fact]
    public void ExecutionError_Properties_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var error = new ExecutionError("test");

        // Assert
        error.ErrorCode.ShouldBeNull();
        error.LogLevel.ShouldBe(LogLevel.Error);
        error.Logged.ShouldBeFalse();
    }

    [Fact]
    public void ExecutionError_Properties_ShouldBeSettable()
    {
        // Arrange
        var error = new ExecutionError("test");

        // Act
        error.ErrorCode = "NOT_FOUND";
        error.LogLevel = LogLevel.Warning;
        error.Properties["resource"] = "customer";

        // Assert
        error.ErrorCode.ShouldBe("NOT_FOUND");
        error.LogLevel.ShouldBe(LogLevel.Warning);
        error.Properties["resource"].ShouldBe("customer");
    }
}

public class ExecutionErrorGenericTests
{
    [Fact]
    public void ExecutionError_Generic_WithCustomType_ShouldWork()
    {
        // Arrange
        var customMessages = new[] { 1, 2, 3 };

        // Act
        var error = new ExecutionError<int>(customMessages);

        // Assert
        error.Message.ShouldBe("1; 2; 3");
        error.Messages.Count.ShouldBe(3);
    }

    [Fact]
    public void ExecutionError_Generic_WithEnumerable_ShouldWork()
    {
        // Arrange
        var customMessages = new List<int> { 100, 200 };

        // Act
        var error = new ExecutionError<int>(customMessages);

        // Assert
        error.Message.ShouldBe("100; 200");
        error.Messages.Count.ShouldBe(2);
    }
}
