namespace FunctionalUseCases.Sample;

/// <summary>
/// Sample use case parameter that demonstrates the UseCase pattern implementation.
/// This use case parameter contains a name for generating a greeting message.
/// </summary>
public class SampleUseCase(string name) : IUseCaseParameter<string>
{
    /// <summary>
    /// Gets the name to greet.
    /// </summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
}
