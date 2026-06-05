namespace FunctionalUseCases.Tests;

public class ExecutionExceptionTests
{
    [Fact]
    public void ExecutionException_Constructor_ShouldSetMessage()
    {
        // Arrange
        const string message = "Test exception message";

        // Act
        var exception = new ExecutionException(message);

        // Assert
        exception.Message.ShouldBe(message);
    }

    [Fact]
    public void ExecutionException_ShouldBeSerializable()
    {
        // Arrange
        const string message = "Test exception message";
        var exception = new ExecutionException(message);

        // Act & Assert - Should not throw
        // The [Serializable] attribute is applied to the class
        exception.GetType().GetCustomAttributes(typeof(SerializableAttribute), false).Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ExecutionException_ShouldInheritFromException()
    {
        // Arrange
        var exception = new ExecutionException("test");

        // Act & Assert
        exception.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    public void ExecutionException_Constructor_ShouldSetInnerException()
    {
        var innerException = new InvalidOperationException("Original");

        var exception = new ExecutionException("Wrapped", innerException);

        exception.InnerException.ShouldBeSameAs(innerException);
    }
}
