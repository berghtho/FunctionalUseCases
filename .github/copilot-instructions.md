# FunctionalUseCases .NET Library

Always reference these instructions first and fall back to search or bash only when something doesn't match.

## Working Effectively

### Bootstrap and Build Process
- Git setup (critical): run `git fetch --unshallow` before any build to satisfy Nerdbank.GitVersioning.
- Build and test:
  - `git fetch --unshallow`
  - `dotnet restore` (~15s first run)
  - `dotnet build --no-restore` (~8s)
  - `dotnet test --no-build --verbosity minimal` (~1-3s, 92 tests)
- Release builds:
  - `dotnet build --configuration Release`
  - `dotnet test --configuration Release`
- Code formatting:
  - `dotnet format`
  - `dotnet format --verify-no-changes`
- NuGet packaging:
  - `dotnet pack FunctionalUseCases/FunctionalUseCases.csproj --configuration Release --output ./artifacts`

### Timeout Requirements - NEVER CANCEL
- Use 120s+ timeouts for long operations.
- git fetch --unshallow: 60s; dotnet restore: 120s; dotnet build: 60s; dotnet test: 60s; dotnet format: 60s.

## Validation Scenarios

1) Complete build cycle:
```
git fetch --unshallow
dotnet clean
dotnet restore
dotnet build --no-restore
dotnet test --no-build --verbosity minimal
dotnet format --verify-no-changes
```

2) Sample application:
```
cd Sample
dotnet run
```
- Expect success greeting for a valid name and a clear error for empty names.
- Chain example should pass result between steps.
- To exercise the WithBehavior transaction samples instead of the expected "service missing" message, register the open generic behavior and transaction manager: `services.AddScoped<ITransactionManager, SampleTransactionManager>(); services.AddScoped(typeof(TransactionBehavior<,>));`.

3) Release build validation:
```
dotnet build --configuration Release
dotnet test --configuration Release
```

## Project Structure and Key Locations

```
FunctionalUseCases/
├── FunctionalUseCases.sln
├── FunctionalUseCases/
│   ├── Execution.cs / ExecutionError.cs / ExecutionException.cs / ExecutionResult.cs
│   ├── ExecutionContext.cs                        # Per-call behavior pipeline
│   ├── PipelineBehaviorDelegate.cs
│   ├── UseCaseDispatcher.cs                       # Global behavior pipeline
│   ├── UseCaseChain.cs                            # Chain and chain-aware execution
│   ├── TransactionBehavior.cs                     # Scoped chain-aware transaction behavior
│   ├── Interfaces/
│   │   ├── IUseCase.cs / IUseCaseDispatcher.cs
│   │   ├── IExecutionBehavior.cs / IScopedExecutionBehavior.cs
│   │   ├── IExecutionScope.cs / ITransactionManager.cs
│   ├── Extensions/
│   │   ├── ExecutionBehaviorExtensions.cs        # dispatcher.WithBehavior(...)
│   │   ├── ExecutionResultExtensions.cs
│   │   ├── UseCaseChainExtensions.cs             # dispatcher.StartWith(...)
│   │   └── UseCaseRegistrationExtensions.cs      # DI scanning helpers
│   └── Sample/
│       ├── SampleUseCase.cs / SampleUseCaseHandler.cs
│       ├── LoggingBehavior.cs / TimingBehavior.cs
│       ├── SampleTransactionManager.cs / ChainingExample.cs
├── Sample/Program.cs                               # Console demo
├── FunctionalUseCases.Tests/                      # xUnit tests (92)
├── version.json                                   # Nerdbank.GitVersioning config
└── README.md
```

## Technology Stack and Dependencies
- Target framework: .NET 10.0
- Microsoft.Extensions.DependencyInjection 10.0.0
- Microsoft.Extensions.Logging.Abstractions 10.0.0
- Scrutor 5.0.1
- Nerdbank.GitVersioning 3.7.115
- Test stack: xUnit 2.4.2, Shouldly 4.3.0, FakeItEasy 8.3.0, Microsoft.NET.Test.Sdk 17.11.0

## Key Patterns and Concepts

### Use Case Pattern
- `IUseCaseParameter<TResult>` marks inputs; `IUseCase<TParameter, TResult>` implements logic.
- `ExecutionResult<TResult>`/`ExecutionResult` carry success/failure with rich `ExecutionError`; create via `Execution.Success(...)` or `Execution.Failure(...)` and combine results with `+`/`Execution.Combine`.

### Execution Behaviors
- Global behaviors: register with DI (`services.AddScoped(typeof(IExecutionBehavior<,>), typeof(MyBehavior<,>));`) and they run in registration order.
- Per-call behaviors: `dispatcher.WithBehavior(typeof(OpenGeneric<,>))` or `.WithBehavior(instance)`; open generic behaviors must be registered with DI and run before global behaviors.
- Scoped behaviors: implement `IScopedExecutionBehavior`/`ScopedExecutionBehavior` to access `IExecutionScope` (chain detection, start/end flags). `TransactionBehavior<,>` is chain-aware and requires an `ITransactionManager` registration.

### Use Case Chains
- Build chains with `dispatcher.StartWith(...)` then `.Then(...)` to pass results between steps; `.OnError(...)` handles failures; `.ExecuteAsync()` runs the chain.
- Chain-level `.WithBehavior(...)` applies per-call behaviors to every step; optional `ITransactionManager` argument on `StartWith` creates a single transaction for the chain.

### Behavior Registration Notes
- Behaviors are never auto-discovered; register both global behaviors and any open generic behaviors needed for `WithBehavior`.
- Ensure dependencies (e.g., `ITransactionManager` for `TransactionBehavior<,>`) are registered before using per-call behaviors or chain-level transactions.

## Known Issues and Workarounds
- Shallow clones break versioning: always run `git fetch --unshallow` before builds/tests.
- Formatting: use `dotnet format` (or `--verify-no-changes`) to resolve style issues.
- Missing behavior registrations cause DI errors; register required open generic behaviors and dependencies when using `WithBehavior` or transaction samples.

## Performance Expectations
- Restore ~15s on first run, build ~8s, tests ~1-3s for 92 tests, format ~10s, sample app runs instantly.
- Full CI cycle (restore + build + test + format verify) typically completes in under a minute after packages are cached.
