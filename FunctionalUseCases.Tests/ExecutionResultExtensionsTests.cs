using Microsoft.Extensions.Logging;
using FunctionalUseCases.Extensions;

namespace FunctionalUseCases.Tests;

public class ExecutionResultExtensionsTests
{
    [Fact]
    public void NoLog_ShouldSetNoLogProperty()
    {
        // Arrange
        var result = Execution.Success();

        // Act
        var noLogResult = result.NoLog();

        // Assert
        noLogResult.NoLog.ShouldNotBeNull();
        noLogResult.NoLog.Value.ShouldBeTrue();
        noLogResult.ShouldBeSameAs(result); // Should return the same instance
    }

    [Fact]
    public void AsTask_ShouldReturnCompletedTask()
    {
        // Arrange
        var result = Execution.Success("test value");

        // Act
        var task = result.AsTask();

        // Assert
        task.IsCompleted.ShouldBeTrue();
        task.Result.ShouldBe(result);
    }

    [Fact]
    public void Log_WithSuccessfulResult_ShouldNotLog()
    {
        // Arrange
        var result = Execution.Success();
        var mockLogger = A.Fake<ILogger>();

        // Act
        var loggedResult = result.Log(mockLogger);

        // Assert
        loggedResult.ShouldBeSameAs(result);
        A.CallTo(mockLogger).MustNotHaveHappened();
    }

    [Fact]
    public void Log_WithFailedResult_ShouldLogError()
    {
        // Arrange
        const string errorMessage = "Test error";
        var result = Execution.Failure(errorMessage);
        var mockLogger = A.Fake<ILogger>();
        A.CallTo(() => mockLogger.IsEnabled(A<LogLevel>._)).Returns(true);

        // Act
        var loggedResult = result.Log(mockLogger);

        // Assert
        loggedResult.ShouldBeSameAs(result);
        A.CallTo(mockLogger)
            .Where(call => call.Method.Name == "Log" &&
                          call.GetArgument<LogLevel>(0) == LogLevel.Error)
            .MustHaveHappened();
        result.Error!.Logged.ShouldBeTrue();
    }

    [Fact]
    public void Log_WithAlreadyLoggedResult_ShouldNotLogAgain()
    {
        // Arrange
        const string errorMessage = "Test error";
        var result = Execution.Failure(errorMessage);
        var mockLogger = A.Fake<ILogger>();

        // Log it first to set the Logged flag
        result.Log(mockLogger);
        Fake.ClearRecordedCalls(mockLogger); // Clear the recorded calls to test the second call

        // Act - log again
        var loggedResult = result.Log(mockLogger);

        // Assert
        loggedResult.ShouldBeSameAs(result);
        A.CallTo(mockLogger).MustNotHaveHappened(); // Should not log again
    }

    [Fact]
    public void Log_WithDifferentLogLevels_ShouldLogAtCorrectLevel()
    {
        // Arrange
        var warningResult = Execution.Failure("Warning message", logLevel: LogLevel.Warning);
        var infoResult = Execution.Failure("Info message", logLevel: LogLevel.Information);
        var mockLogger = A.Fake<ILogger>();
        A.CallTo(() => mockLogger.IsEnabled(A<LogLevel>._)).Returns(true);

        // Act
        warningResult.Log(mockLogger);
        infoResult.Log(mockLogger);

        // Assert
        A.CallTo(mockLogger)
            .Where(call => call.Method.Name == "Log" &&
                          call.GetArgument<LogLevel>(0) == LogLevel.Warning)
            .MustHaveHappened();

        A.CallTo(mockLogger)
            .Where(call => call.Method.Name == "Log" &&
                          call.GetArgument<LogLevel>(0) == LogLevel.Information)
            .MustHaveHappened();
    }

    [Fact]
    public void Log_WithFailedResult_UsingTestLogger_ShouldLogCorrectMessage()
    {
        // Arrange
        const string errorMessage = "Test error";
        var result = Execution.Failure(errorMessage);
        var testLogger = new TestLogger();

        // Act
        var loggedResult = result.Log(testLogger);

        // Assert
        loggedResult.ShouldBeSameAs(result);
        testLogger.LoggedMessages.ShouldNotBeEmpty();
        testLogger.LoggedMessages.ShouldContain(m => m.Message.Contains(errorMessage) && m.LogLevel == LogLevel.Error);
        result.Error!.Logged.ShouldBeTrue();
    }

    private class TestLogger : ILogger
    {
        public List<LogEntry> LoggedMessages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LoggedMessages.Add(new LogEntry
            {
                LogLevel = logLevel,
                Message = formatter(state, exception)
            });
        }

        public class LogEntry
        {
            public LogLevel LogLevel { get; set; }
            public string Message { get; set; } = string.Empty;
        }
    }
}
