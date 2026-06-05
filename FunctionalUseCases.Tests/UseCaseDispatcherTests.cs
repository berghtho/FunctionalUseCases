using Microsoft.Extensions.DependencyInjection;

namespace FunctionalUseCases.Tests;

public class UseCaseDispatcherTests
{
    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new UseCaseDispatcher(null!));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullParameter_ShouldThrowArgumentNullException()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new UseCaseDispatcher(serviceProvider);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() =>
           dispatcher.ExecuteAsync<string>(null!));
    }

    [Fact]
    public async Task ExecuteAsync_WithUnregisteredUseCase_ShouldReturnFailure()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new UseCaseDispatcher(serviceProvider);
        var parameter = new TestUseCaseParameter();

        // Act
        var result = await dispatcher.ExecuteAsync<string>(parameter);

        // Assert
        result.ExecutionSucceeded.ShouldBeFalse();
        result.ExecutionFailed.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldContain("No use case registered");
    }

    [Fact]
    public async Task ExecuteAsync_WithRegisteredUseCase_ShouldExecuteSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IUseCase<TestUseCaseParameter, string>, TestUseCase>();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new UseCaseDispatcher(serviceProvider);
        var parameter = new TestUseCaseParameter();

        // Act
        var result = await dispatcher.ExecuteAsync<string>(parameter);

        // Assert
        result.ExecutionSucceeded.ShouldBeTrue();
        result.CheckedValue.ShouldBe("Test Result");
    }

    [Fact]
    public async Task ExecuteAsync_WithBehavior_ShouldExecuteThroughBehavior()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IUseCase<TestUseCaseParameter, string>, TestUseCase>();
        services.AddTransient<IExecutionBehavior<TestUseCaseParameter, string>, TestBehavior>();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new UseCaseDispatcher(serviceProvider);
        var parameter = new TestUseCaseParameter();

        // Act
        var result = await dispatcher.ExecuteAsync<string>(parameter);

        // Assert
        result.ExecutionSucceeded.ShouldBeTrue();
        result.CheckedValue.ShouldBe("Behavior: Test Result");
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleBehaviors_ShouldExecuteInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IUseCase<TestUseCaseParameter, string>, TestUseCase>();
        services.AddTransient<IExecutionBehavior<TestUseCaseParameter, string>, TestBehavior>();
        services.AddTransient<IExecutionBehavior<TestUseCaseParameter, string>, TestBehavior2>();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new UseCaseDispatcher(serviceProvider);
        var parameter = new TestUseCaseParameter();

        // Act
        var result = await dispatcher.ExecuteAsync<string>(parameter);

        // Assert
        result.ExecutionSucceeded.ShouldBeTrue();
        // Due to reverse order wrapping in the pipeline, TestBehavior executes first, then TestBehavior2
        result.CheckedValue.ShouldBe("Behavior: Behavior2: Test Result");
    }

    [Fact]
    public async Task ExecuteAsync_WithMockedUseCase_ShouldCallUseCase()
    {
        // Arrange - Using FakeItEasy to create mocks
        var mockUseCase = A.Fake<IUseCase<TestUseCaseParameter, string>>();
        var expectedResult = Execution.Success("Mocked Result");

        A.CallTo(() => mockUseCase.ExecuteAsync(A<TestUseCaseParameter>._, A<CancellationToken>._))
            .Returns(Task.FromResult(expectedResult));

        var services = new ServiceCollection();
        services.AddSingleton(mockUseCase);
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new UseCaseDispatcher(serviceProvider);
        var parameter = new TestUseCaseParameter();

        // Act
        var result = await dispatcher.ExecuteAsync<string>(parameter);

        // Assert
        result.ExecutionSucceeded.ShouldBeTrue();
        result.CheckedValue.ShouldBe("Mocked Result");

        // Verify the use case was called with the correct parameter
        A.CallTo(() => mockUseCase.ExecuteAsync(parameter, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_WithMockedServiceProvider_ShouldHandleServiceResolution()
    {
        // Arrange - Using FakeItEasy to mock service provider behavior
        var mockServiceProvider = A.Fake<IServiceProvider>();
        var testUseCase = new TestUseCase();

        // Configure the mock to return our test use case
        A.CallTo(() => mockServiceProvider.GetService(typeof(IUseCase<TestUseCaseParameter, string>)))
            .Returns(testUseCase);
        A.CallTo(() => mockServiceProvider.GetService(typeof(IEnumerable<IExecutionBehavior<TestUseCaseParameter, string>>)))
            .Returns(Enumerable.Empty<IExecutionBehavior<TestUseCaseParameter, string>>());

        var dispatcher = new UseCaseDispatcher(mockServiceProvider);
        var parameter = new TestUseCaseParameter();

        // Act
        var result = await dispatcher.ExecuteAsync<string>(parameter);

        // Assert
        result.ExecutionSucceeded.ShouldBeTrue();
        result.CheckedValue.ShouldBe("Test Result");

        // Verify service provider was called to resolve the use case
        A.CallTo(() => mockServiceProvider.GetService(typeof(IUseCase<TestUseCaseParameter, string>)))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUseCaseThrows_ShouldPreserveOriginalException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IUseCase<ThrowingUseCaseParameter, string>, ThrowingUseCase>();
        var dispatcher = new UseCaseDispatcher(services.BuildServiceProvider());

        // Act
        var result = await dispatcher.ExecuteAsync<string>(new ThrowingUseCaseParameter());

        // Assert
        result.ExecutionFailed.ShouldBeTrue();
        result.Error!.Exception.ShouldBeOfType<InvalidOperationException>();
        result.Error.Exception.ShouldNotBeOfType<System.Reflection.TargetInvocationException>();
        result.Error.Exception.StackTrace.ShouldNotBeNull();
        result.Error.Exception.StackTrace.ShouldContain(nameof(ThrowingUseCase.ExecuteAsync));
    }

    // Test helper classes
    public class TestUseCaseParameter : IUseCaseParameter<string>
    {
    }

    public class TestUseCase : IUseCase<TestUseCaseParameter, string>
    {
        public Task<ExecutionResult<string>> ExecuteAsync(TestUseCaseParameter useCaseParameter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Execution.Success("Test Result"));
        }
    }

    public class TestBehavior : IExecutionBehavior<TestUseCaseParameter, string>
    {
        public async Task<ExecutionResult<string>> ExecuteAsync(TestUseCaseParameter useCaseParameter, PipelineBehaviorDelegate<string> next, CancellationToken cancellationToken = default)
        {
            var result = await next();
            if (result.ExecutionSucceeded)
            {
                return Execution.Success("Behavior: " + result.CheckedValue);
            }
            return result;
        }
    }

    public class TestBehavior2 : IExecutionBehavior<TestUseCaseParameter, string>
    {
        public async Task<ExecutionResult<string>> ExecuteAsync(TestUseCaseParameter useCaseParameter, PipelineBehaviorDelegate<string> next, CancellationToken cancellationToken = default)
        {
            var result = await next();
            if (result.ExecutionSucceeded)
            {
                return Execution.Success("Behavior2: " + result.CheckedValue);
            }
            return result;
        }
    }

    public class ThrowingUseCaseParameter : IUseCaseParameter<string>
    {
    }

    public class ThrowingUseCase : IUseCase<ThrowingUseCaseParameter, string>
    {
        public Task<ExecutionResult<string>> ExecuteAsync(
            ThrowingUseCaseParameter useCaseParameter,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Handler failed");
    }
}
