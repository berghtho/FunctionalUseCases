# FunctionalUseCases

A complete .NET solution that implements functional processing of use cases using the Mediator pattern with advanced ExecutionResult error handling. This library provides a clean way to organize business logic into discrete, testable use cases with sophisticated dependency injection support and functional error handling patterns.

## Features

- 🎯 **Mediator Pattern**: Clean separation between use case parameters and their implementations
- 🚀 **Dependency Injection**: Full support for Microsoft.Extensions.DependencyInjection
- 🔍 **Automatic Registration**: Use Scrutor to automatically discover and register use cases
- ✅ **Advanced ExecutionResult Pattern**: Sophisticated functional approach with both generic and non-generic variants
- 🛡️ **Rich Error Handling**: ExecutionError with multiple messages, error codes, and log levels
- 🔄 **Implicit Conversions**: Seamless conversion between values and ExecutionResult
- ➕ **Result Combination**: Combine multiple ExecutionResult objects using the `+` operator or `Combine()` method
- 🧪 **Testable**: Easy to unit test individual use cases with comprehensive error scenarios
- 📦 **Enterprise-Ready**: Robust implementation with logging integration and cancellation support
- 🔗 **Global Execution Behaviors**: Cross-cutting concerns like logging, validation, caching applied to all use case executions
- ⚡ **Per-Call Execution Behaviors**: Fluent API for applying behaviors to specific use case executions or chains using `WithBehavior(typeof(MyBehavior<,>))`
- 🔄 **Use Case Chaining**: Fluent chain execution with result passing and chain-aware behavior support
- 🏷️ **Chain-Aware Transaction Management**: Intelligent transaction handling that adapts based on execution context (single use case vs. chain)

## Installation

Add the required packages to your project:

```bash
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Logging.Abstractions
dotnet add package Scrutor
```

## Quick Start

### 1. Define a Use Case Parameter

```csharp
using FunctionalUseCases;

public class GreetUserUseCase : IUseCaseParameter<string>
{
    public string Name { get; }

    public GreetUserUseCase(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}
```

### 2. Create a Use Case Implementation

```csharp
using FunctionalUseCases;

public class GreetUserUseCaseHandler : IUseCase<GreetUserUseCase, string>
{
    public async Task<ExecutionResult<string>> ExecuteAsync(GreetUserUseCase useCaseParameter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(useCaseParameter.Name))
        {
            return Execution.Failure<string>("Name cannot be empty");
        }

        var greeting = $"Hello, {useCaseParameter.Name}!";
        return Execution.Success(greeting);
    }
}
```

### 3. Register Services

```csharp
using Microsoft.Extensions.DependencyInjection;
using FunctionalUseCases;

var services = new ServiceCollection();

// Register all use cases from the assembly containing GreetUserUseCase
services.AddUseCasesFromAssemblyContaining<GreetUserUseCase>();

var serviceProvider = services.BuildServiceProvider();
```

### 4. Execute Use Cases

```csharp
var dispatcher = serviceProvider.GetRequiredService<IUseCaseDispatcher>();

var useCaseParameter = new GreetUserUseCase("World");
var result = await dispatcher.ExecuteAsync(useCaseParameter);

if (result.ExecutionSucceeded)
{
    Console.WriteLine(result.CheckedValue); // Output: Hello, World!
}
else
{
    Console.WriteLine($"Error: {result.Error?.Message}");
}
```

## Core Components

### IUseCaseParameter Interface

Marker interface for use case parameters. All use case parameters should implement `IUseCaseParameter<TResult>`:

```csharp
public interface IUseCaseParameter<out TResult> : IUseCaseParameter
{
}
```

*Located in: `FunctionalUseCases/Interfaces/IUseCase.cs`*

### IUseCase Interface

Generic interface for use case implementations that process use case parameters:

```csharp
public interface IUseCase<in TUseCaseParameter, TResult>
    where TUseCaseParameter : IUseCaseParameter<TResult>
    where TResult : notnull
{
    Task<ExecutionResult<TResult>> ExecuteAsync(TUseCaseParameter useCaseParameter, CancellationToken cancellationToken = default);
}
```

*Located in: `FunctionalUseCases/Interfaces/IUseCase.cs`*

### ExecutionResult<T> and ExecutionResult

Advanced functional result types that encapsulate success/failure with rich error information:

```csharp
// Generic variant
public record ExecutionResult<T>(ExecutionError? Error = null) : ExecutionResult(Error) where T : notnull
{
    public bool ExecutionSucceeded { get; }
    public bool ExecutionFailed { get; }
    public T CheckedValue { get; } // Throws if failed
}

// Non-generic variant
public record ExecutionResult(ExecutionError? Error = null)
{
    public bool ExecutionSucceeded { get; }
    public bool ExecutionFailed { get; }
    public ExecutionError CheckedError { get; }
}

// Factory methods via Execution class
var success = Execution.Success("Hello World");
var failure = Execution.Failure<string>("Something went wrong");
var failureWithException = Execution.Failure<string>("Error message", exception);

// Implicit conversion
ExecutionResult<string> result = "Hello World"; // Automatically creates success result
```

### ExecutionError

Rich error information with support for multiple messages, error codes, and logging levels:

```csharp
public record ExecutionError(
    string Message,
    string? ErrorCode = null,
    LogLevel LogLevel = LogLevel.Error,
    Exception? Exception = null,
    IDictionary<string, object>? Properties = null
);
```

### IUseCaseDispatcher

Mediator that resolves and executes use cases:

```csharp
public interface IUseCaseDispatcher
{
    Task<ExecutionResult<TResult>> ExecuteAsync<TResult>(IUseCaseParameter<TResult> useCaseParameter, CancellationToken cancellationToken = default)
        where TResult : notnull;
}
```

*Located in: `FunctionalUseCases/Interfaces/IUseCaseDispatcher.cs`*

## Global Execution Behaviors

Global execution behaviors allow you to implement cross-cutting concerns like logging, validation, caching, performance monitoring, and more. They wrap around all use case executions in a clean, composable way and are registered globally via dependency injection.

### IExecutionBehavior Interface

```csharp
public interface IExecutionBehavior<in TUseCaseParameter, TResult>
    where TUseCaseParameter : IUseCaseParameter<TResult>
    where TResult : notnull
{
    Task<ExecutionResult<TResult>> ExecuteAsync(TUseCaseParameter useCaseParameter, PipelineBehaviorDelegate<TResult> next, CancellationToken cancellationToken = default);
}
```

*Located in: `FunctionalUseCases/Interfaces/IExecutionBehavior.cs`*

### Creating an Execution Behavior

```csharp
using Microsoft.Extensions.Logging;

public class LoggingBehavior<TUseCaseParameter, TResult> : IExecutionBehavior<TUseCaseParameter, TResult>
    where TUseCaseParameter : IUseCaseParameter<TResult>
    where TResult : notnull
{
    private readonly ILogger<LoggingBehavior<TUseCaseParameter, TResult>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TUseCaseParameter, TResult>> logger)
    {
        _logger = logger;
    }

    public async Task<ExecutionResult<TResult>> ExecuteAsync(TUseCaseParameter useCaseParameter, PipelineBehaviorDelegate<TResult> next, CancellationToken cancellationToken = default)
    {
        var useCaseParameterName = typeof(TUseCaseParameter).Name;
        
        _logger.LogInformation("Starting execution of use case: {UseCaseParameterName}", useCaseParameterName);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var result = await next().ConfigureAwait(false);
            
            stopwatch.Stop();
            
            if (result.ExecutionSucceeded)
            {
                _logger.LogInformation("Successfully executed use case: {UseCaseParameterName} in {ElapsedMilliseconds}ms", 
                    useCaseParameterName, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("Use case execution failed: {UseCaseParameterName} in {ElapsedMilliseconds}ms. Error: {ErrorMessage}", 
                    useCaseParameterName, stopwatch.ElapsedMilliseconds, result.Error?.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Exception occurred during use case execution: {UseCaseParameterName} in {ElapsedMilliseconds}ms", 
                useCaseParameterName, stopwatch.ElapsedMilliseconds);
            
            return Execution.Failure<TResult>($"Exception in LoggingBehavior: {ex.Message}", ex);
        }
    }
}
```

### Manual Registration

Global execution behaviors are NOT automatically registered when you call the registration extension methods. You must register them manually and they will be applied to all use case executions:

```csharp
// Register use cases from assembly
services.AddUseCasesFromAssemblyContaining<GreetUserUseCase>();

// Register global execution behaviors manually - these apply to ALL use case executions
services.AddScoped(typeof(IExecutionBehavior<,>), typeof(LoggingBehavior<,>));
services.AddScoped(typeof(IExecutionBehavior<,>), typeof(TimingBehavior<,>));
```

### Execution Order

Global behaviors are executed in the order they are registered. Each behavior can execute logic before and after the next step in the pipeline:

```
Behavior 1 (before) → Behavior 2 (before) → Use Case Handler → Behavior 2 (after) → Behavior 1 (after)
```

### Common Global Execution Behavior Patterns

**Validation Behavior:**
```csharp
public class ValidationBehavior<TUseCaseParameter, TResult> : IExecutionBehavior<TUseCaseParameter, TResult>
    where TUseCaseParameter : IUseCaseParameter<TResult>
    where TResult : notnull
{
    public async Task<ExecutionResult<TResult>> ExecuteAsync(TUseCaseParameter useCaseParameter, PipelineBehaviorDelegate<TResult> next, CancellationToken cancellationToken = default)
    {
        // Perform validation logic
        if (/* validation fails */)
        {
            return Execution.Failure<TResult>("Validation failed");
        }
        
        return await next().ConfigureAwait(false);
    }
}
```

**Caching Behavior:**
```csharp
public class CachingBehavior<TUseCaseParameter, TResult> : IExecutionBehavior<TUseCaseParameter, TResult>
    where TUseCaseParameter : IUseCaseParameter<TResult>
    where TResult : notnull
{
    private readonly IMemoryCache _cache;

    public async Task<ExecutionResult<TResult>> ExecuteAsync(TUseCaseParameter useCaseParameter, PipelineBehaviorDelegate<TResult> next, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{typeof(TUseCaseParameter).Name}_{useCaseParameter.GetHashCode()}";
        
        if (_cache.TryGetValue(cacheKey, out ExecutionResult<TResult> cachedResult))
        {
            return cachedResult;
        }
        
        var result = await next().ConfigureAwait(false);
        
        if (result.ExecutionSucceeded)
        {
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        }
        
        return result;
    }
}
```

**Transaction Behavior:**
```csharp
public class TransactionBehavior<TUseCaseParameter, TResult> : IExecutionBehavior<TUseCaseParameter, TResult>
    where TUseCaseParameter : IUseCaseParameter<TResult>
    where TResult : notnull
{
    private readonly ITransactionManager _transactionManager;
    private readonly ILogger<TransactionBehavior<TUseCaseParameter, TResult>> _logger;

    public TransactionBehavior(ITransactionManager transactionManager, ILogger<TransactionBehavior<TUseCaseParameter, TResult>> logger)
    {
        _transactionManager = transactionManager;
        _logger = logger;
    }

    public async Task<ExecutionResult<TResult>> ExecuteAsync(TUseCaseParameter useCaseParameter, PipelineBehaviorDelegate<TResult> next, CancellationToken cancellationToken = default)
    {
        ITransaction? transaction = null;
        try
        {
            // Begin transaction
            transaction = await _transactionManager.BeginTransactionAsync(cancellationToken);
            
            // Execute the use case
            var result = await next().ConfigureAwait(false);

            if (result.ExecutionSucceeded)
            {
                // Commit transaction on success
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                // Rollback transaction on failure
                await transaction.RollbackAsync(cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            // Rollback transaction on exception
            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction");
                    // Don't throw rollback exception, preserve original exception
                }
            }

            return Execution.Failure<TResult>($"Exception in TransactionBehavior: {ex.Message}", ex);
        }
        finally
        {
            // Ensure transaction is disposed
            transaction?.Dispose();
        }
    }
}
```

To use the transaction behavior, implement the `ITransactionManager` interface for your specific database technology:

```csharp
// Example Entity Framework implementation
public class EntityFrameworkTransactionManager : ITransactionManager
{
    private readonly DbContext _context;

    public EntityFrameworkTransactionManager(DbContext context)
    {
        _context = context;
    }

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new EntityFrameworkTransaction(transaction);
    }
}

public class EntityFrameworkTransaction : ITransaction
{
    private readonly IDbContextTransaction _transaction;

    public EntityFrameworkTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.RollbackAsync(cancellationToken);
    }

    public void Dispose()
    {
        _transaction.Dispose();
    }
}

// Register the transaction behavior and manager
services.AddScoped<ITransactionManager, EntityFrameworkTransactionManager>();
services.AddScoped(typeof(IExecutionBehavior<,>), typeof(TransactionBehavior<,>));
```

*Located in: `FunctionalUseCases/TransactionBehavior.cs` and `FunctionalUseCases/Interfaces/ITransactionManager.cs`*

## Per-Call Execution Behaviors (WithBehavior API)

In addition to global behaviors that apply to all use case executions, the library provides a powerful fluent API for applying behaviors to specific use case executions or chains. This allows for fine-grained control over when and where behaviors are applied.

### Two Types of Behaviors

The system now supports two distinct behavior application patterns:

1. **Global Behaviors**: Registered with dependency injection and applied to ALL use case executions
2. **Per-Call Behaviors**: Applied to specific executions using the `WithBehavior()` fluent API with open generic types

### WithBehavior() Fluent API

The `WithBehavior()` method allows you to apply behaviors to specific use case executions using open generic type definitions. This approach ensures behaviors remain cross-cutting concerns that work with any use case parameter and result types.

#### Single Use Case with Behavior

```csharp
// Apply a transaction behavior to a specific use case execution
var result = await dispatcher
    .WithBehavior(typeof(TransactionBehavior<,>))
    .ExecuteAsync(new MyUseCase("data"));

// Apply multiple behaviors to the same execution
var result = await dispatcher
    .WithBehavior(typeof(TransactionBehavior<,>))
    .WithBehavior(typeof(ValidationBehavior<,>))
    .ExecuteAsync(new MyUseCase("data"));

// Use behavior instances instead of types
var customBehavior = new CustomBehavior<MyUseCase, string>(someParameter);
var result = await dispatcher
    .WithBehavior(customBehavior)
    .ExecuteAsync(new MyUseCase("data"));
```

#### Use Case Chains with Behaviors

```csharp
// Apply behavior to an entire use case chain
var result = await dispatcher
    .StartWith(new FirstUseCase("initial"))
    .WithBehavior(typeof(TransactionBehavior<,>))
    .Then(x => new SecondUseCase(x.Id, x.Property))
    .Then(x => new ThirdUseCase(x.ProcessedData))
    .ExecuteAsync();

// Apply multiple behaviors to a chain
var result = await dispatcher
    .StartWith(new GetUserUseCase(userId))
    .WithBehavior(typeof(TransactionBehavior<,>))
    .WithBehavior(typeof(ValidationBehavior<,>))
    .Then(user => new ValidateUserUseCase(user))
    .Then(user => new SendWelcomeEmailUseCase(user.Email, user.Name))
    .ExecuteAsync();

// Behaviors can be added at any point in the chain
var result = await dispatcher
    .StartWith(new FirstUseCase())
    .Then(x => new SecondUseCase(x.Id))
    .WithBehavior(typeof(TransactionBehavior<,>))
    .Then(x => new ThirdUseCase(x.ProcessedData))
    .ExecuteAsync();
```

### Chain-Aware Transaction Behavior

The `TransactionBehavior<TUseCaseParameter, TResult>` is a sophisticated example of a chain-aware behavior that adapts its strategy based on the execution context:

#### Intelligent Transaction Management

- **Single Use Case**: Creates transaction at use case start → commits/rollbacks at use case end
- **Chain Execution**: Creates transaction at chain start → commits/rollbacks at chain end
- **Automatic Detection**: Uses `IExecutionScope` to determine context without user intervention

#### Example Transaction Behavior Usage

```csharp
// Transaction per single use case
var result = await dispatcher
    .WithBehavior(typeof(TransactionBehavior<,>))
    .ExecuteAsync(new CreateOrderUseCase(orderData));
// Creates transaction → executes use case → commits/rollbacks transaction

// Transaction per entire chain
var result = await dispatcher
    .StartWith(new CreateOrderUseCase(orderData))
    .WithBehavior(typeof(TransactionBehavior<,>))
    .Then(order => new ReserveInventoryUseCase(order.Items))
    .Then(inventory => new ProcessPaymentUseCase(orderData.Payment))
    .Then(payment => new SendConfirmationEmailUseCase(order.CustomerEmail))
    .ExecuteAsync();
// Creates transaction → executes entire chain → commits/rollbacks transaction
```

### Creating Chain-Aware Behaviors

To create behaviors that adapt to execution context, implement `IScopedExecutionBehavior<TUseCaseParameter, TResult>` instead of the base `IExecutionBehavior<TUseCaseParameter, TResult>`:

```csharp
using Microsoft.Extensions.Logging;

public class CustomTransactionBehavior<TUseCaseParameter, TResult> : ScopedExecutionBehavior<TUseCaseParameter, TResult>
    where TUseCaseParameter : IUseCaseParameter<TResult>
    where TResult : notnull
{
    private readonly ITransactionManager _transactionManager;
    private readonly ILogger _logger;

    public CustomTransactionBehavior(ITransactionManager transactionManager, ILogger logger)
    {
        _transactionManager = transactionManager;
        _logger = logger;
    }

    public override async Task<ExecutionResult<TResult>> ExecuteAsync(
        TUseCaseParameter useCaseParameter, 
        IExecutionScope scope, 
        PipelineBehaviorDelegate<TResult> next, 
        CancellationToken cancellationToken = default)
    {
        if (scope.IsChainExecution)
        {
            // Chain execution logic
            if (scope.IsChainStart)
            {
                _logger.LogInformation("Starting transaction for chain {ChainId}", scope.ChainId);
                // Start transaction for entire chain
            }
            
            var result = await next().ConfigureAwait(false);
            
            if (scope.IsChainEnd)
            {
                // Commit or rollback transaction at chain end
                if (result.ExecutionSucceeded)
                {
                    _logger.LogInformation("Committing transaction for chain {ChainId}", scope.ChainId);
                    // Commit transaction
                }
                else
                {
                    _logger.LogWarning("Rolling back transaction for chain {ChainId}", scope.ChainId);
                    // Rollback transaction
                }
            }
            
            return result;
        }
        else
        {
            // Single use case execution logic
            _logger.LogInformation("Starting transaction for single use case");
            // Create transaction → execute → commit/rollback
            
            var transaction = await _transactionManager.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await next().ConfigureAwait(false);
                
                if (result.ExecutionSucceeded)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                transaction.Dispose();
            }
        }
    }
}
```

### ExecutionScope Interface

The `IExecutionScope` interface provides context information to chain-aware behaviors:

```csharp
public interface IExecutionScope
{
    bool IsChainExecution { get; }     // True if part of a use case chain
    bool IsChainStart { get; }         // True if first use case in chain
    bool IsChainEnd { get; }           // True if last use case in chain
    string? ChainId { get; }           // Unique identifier for the chain
}
```

### Behavior Registration for Per-Call Usage

Per-call behaviors are registered as open generic types and resolved at execution time based on the actual use case parameter and result types:

```csharp
// Register behaviors as open generics for per-call usage
services.AddScoped(typeof(TransactionBehavior<,>));
services.AddScoped(typeof(ValidationBehavior<,>));
services.AddScoped(typeof(CachingBehavior<,>));

// Register any dependencies they need
services.AddScoped<ITransactionManager, EntityFrameworkTransactionManager>();
services.AddMemoryCache(); // For caching behavior

// Global behaviors are still registered the same way
services.AddScoped(typeof(IExecutionBehavior<,>), typeof(LoggingBehavior<,>));
```

### Key Benefits

1. **Selective Application**: Apply expensive behaviors (like transactions) only where needed
2. **Chain-Aware Intelligence**: Behaviors automatically adapt to single vs. chain execution
3. **Composition**: Combine multiple per-call behaviors for specific scenarios  
4. **Performance**: Avoid overhead of global behaviors when not needed
5. **Flexibility**: Mix global and per-call behaviors as appropriate

### Use Case Examples

**Scenario 1: E-commerce Order Processing**
```csharp
// Transaction behavior applied to entire order workflow
var result = await dispatcher
    .StartWith(new ValidateOrderUseCase(orderRequest))
    .WithBehavior(typeof(TransactionBehavior<,>))
    .Then(order => new ReserveInventoryUseCase(order.Items))
    .Then(reservation => new ProcessPaymentUseCase(reservation.OrderId, orderRequest.Payment))
    .Then(payment => new CreateOrderUseCase(payment.OrderId, payment.Amount))
    .ExecuteAsync();
// Single transaction spans the entire workflow
```

**Scenario 2: Caching Expensive Queries**
```csharp
// Cache only expensive user profile queries
var profile = await dispatcher
    .WithBehavior(typeof(CachingBehavior<,>))
    .ExecuteAsync(new GetUserProfileUseCase(userId));

// Regular user operations don't use caching
var updateResult = await dispatcher
    .ExecuteAsync(new UpdateUserNameUseCase(userId, newName));
```

**Scenario 3: Validation for Critical Operations**
```csharp
// Apply strict validation only to sensitive operations
var result = await dispatcher
    .WithBehavior(typeof(StrictValidationBehavior<,>))
    .WithBehavior(typeof(AuditLogBehavior<,>))
    .ExecuteAsync(new DeleteAccountUseCase(userId, confirmationToken));
```

## Use Case Chaining

The library provides powerful use case chaining capabilities that allow you to compose multiple use cases into a sequential workflow. Results are automatically passed between use cases, and execution stops on the first failure.

### Basic Chain Syntax

```csharp
// Chain multiple use cases with result passing
var result = await dispatcher
    .StartWith(new GetUserUseCase(userId))
    .Then(user => new ValidateUserUseCase(user))
    .Then(user => new SendWelcomeEmailUseCase(user.Email, user.Name))
    .ExecuteAsync();

// Access the final result
if (result.ExecutionSucceeded)
{
    Console.WriteLine($"Welcome email sent: {result.CheckedValue}");
}
```

### Result Passing Between Use Cases

The `Then()` method automatically passes the result of the previous use case to the next:

```csharp
var result = await dispatcher
    .StartWith(new CreateUserUseCase("John", "john@example.com"))
    .Then(user => new AssignRoleUseCase(user.Id, "StandardUser"))
    .Then(userRole => new SendActivationEmailUseCase(userRole.User.Email, userRole.ActivationToken))
    .Then(activation => new LogUserCreationUseCase(activation.UserId, activation.Timestamp))
    .ExecuteAsync();

// Each use case receives the .CheckedValue from the previous use case as its parameter
```

### Error Handling in Chains

Chains stop execution on the first failure and provide comprehensive error handling:

```csharp
var result = await dispatcher
    .StartWith(new ValidateInputUseCase(inputData))
    .Then(validInput => new ProcessDataUseCase(validInput))
    .Then(processedData => new SaveDataUseCase(processedData))
    .OnError(error => 
    {
        // Handle any error that occurred in the chain
        logger.LogError("Chain execution failed: {Error}", error.Message);
        return Task.FromResult(Execution.Failure<SavedData>($"Processing failed: {error.Message}"));
    })
    .ExecuteAsync();

// If any step fails, the OnError handler is called and subsequent steps are skipped
```

### Combining Chains with Behaviors

Chains work seamlessly with both global and per-call behaviors:

```csharp
// Apply transaction behavior to entire chain
var result = await dispatcher
    .StartWith(new BeginOrderUseCase(customerId))
    .WithBehavior(typeof(TransactionBehavior<,>))
    .Then(order => new AddItemsUseCase(order.Id, items))
    .Then(order => new CalculateTotalUseCase(order))
    .Then(order => new ProcessPaymentUseCase(order.Total, paymentInfo))
    .ExecuteAsync();

// Global logging behavior will still apply to all steps
// Transaction behavior will create one transaction for the entire chain
```

### Advanced Chain Patterns

**Conditional Execution:**
```csharp
var result = await dispatcher
    .StartWith(new GetUserUseCase(userId))
    .Then(user => user.IsActive 
        ? new SendNotificationUseCase(user.Id, message)
        : new LogInactiveUserUseCase(user.Id))
    .ExecuteAsync();
```

**Parallel Processing (using multiple chains):**
```csharp
// Execute multiple independent chains
var userTask = dispatcher
    .StartWith(new GetUserUseCase(userId))
    .Then(user => new UpdateLastLoginUseCase(user.Id))
    .ExecuteAsync();

var preferencesTask = dispatcher
    .StartWith(new GetUserPreferencesUseCase(userId))
    .Then(prefs => new ApplyThemeUseCase(prefs.ThemeId))
    .ExecuteAsync();

// Wait for both chains to complete
var userResult = await userTask;
var preferencesResult = await preferencesTask;
```

**Chain Branching:**
```csharp
var result = await dispatcher
    .StartWith(new ProcessOrderUseCase(orderId))
    .Then(order => order.IsExpress 
        ? dispatcher
            .StartWith(new ExpressShippingUseCase(order))
            .Then(shipping => new SendExpressNotificationUseCase(shipping))
            .ExecuteAsync()
        : dispatcher
            .StartWith(new StandardShippingUseCase(order))
            .Then(shipping => new SendStandardNotificationUseCase(shipping))
            .ExecuteAsync())
    .ExecuteAsync();
```

## Registration Options

The library provides several extension methods for registering use cases (*located in: `FunctionalUseCases/Extensions/UseCaseRegistrationExtensions.cs`*):

**Note:** There are two types of execution behaviors that require different registration approaches:

1. **Global behaviors** are NOT automatically registered. Register them manually using standard DI registration - they apply to ALL executions:

```csharp
// Register from specific assemblies
services.AddUseCases(new[] { typeof(MyUseCaseParameter).Assembly });

// Register from calling assembly
services.AddUseCasesFromAssembly();

// Register from assembly containing a specific type
services.AddUseCasesFromAssemblyContaining<MyUseCaseParameter>();

// Specify service lifetime (default is Transient)
services.AddUseCasesFromAssembly(ServiceLifetime.Scoped);

// Register global execution behaviors manually - applied to ALL use case executions
services.AddScoped(typeof(IExecutionBehavior<,>), typeof(LoggingBehavior<,>));
```

2. **Per-call behaviors** used with `WithBehavior()` are registered as open generics:

```csharp
// Register open generic behaviors for per-call usage
services.AddScoped(typeof(TransactionBehavior<,>));
services.AddScoped(typeof(ValidationBehavior<,>));
services.AddScoped<CachingBehavior<GetUserUseCase, User>>();
```

## Advanced ExecutionResult Features

### Implicit Conversions
```csharp
// Implicit conversion from value to success result
ExecutionResult<string> result = "Hello World";

// Explicit failure creation
var failure = Execution.Failure<string>("Something went wrong");
```

### Combining Results
```csharp
// Using the + operator (new feature)
var result1 = Execution.Success();
var result2 = Execution.Failure("Something went wrong");
var combined = result1 + result2; // Will be failure with error message

// Multiple operations
var success1 = Execution.Success("Value1");
var success2 = Execution.Success("Value2");
var failure1 = Execution.Failure<string>("Error1");
var allCombined = success1 + success2 + failure1; // Will be failure with "Error1"

// Using the Combine method directly
var combined = Execution.Combine(result1, result2, result3);
```

### Error Handling Patterns
```csharp
var result = await dispatcher.ExecuteAsync(useCaseParameter);

// Pattern 1: Check success and access value
if (result.ExecutionSucceeded)
{
    var value = result.CheckedValue; // Safe access to value
    Console.WriteLine(value);
}

// Pattern 2: Handle failure
if (result.ExecutionFailed)
{
    var error = result.Error;
    Console.WriteLine($"Error: {error?.Message}");
    
    // Access additional error information
    Console.WriteLine($"Error Code: {error?.ErrorCode}");
    Console.WriteLine($"Log Level: {error?.LogLevel}");
    
    if (error?.Exception != null)
    {
        Console.WriteLine($"Exception: {error.Exception.Message}");
    }
}

// Pattern 3: Throw on failure
result.ThrowIfFailed("Custom error message");
```

### Logging Integration
```csharp
// ExecutionResult integrates with Microsoft.Extensions.Logging
var result = Execution.Failure<string>("Database connection failed", 
    errorCode: "DB_001", 
    logLevel: LogLevel.Critical);

// Use logging extensions
result.LogIfFailed(logger, "Failed to process user request");
```

## Example Use Cases

The library includes a comprehensive sample implementation demonstrating the pattern:

- **SampleUseCase**: Use case parameter containing a name for greeting generation
- **SampleUseCaseHandler**: Use case implementation that processes the parameter with validation and business logic using ExecutionResult API

Run the sample application to see it in action:

```bash
cd Sample
dotnet run
```

### Sample Implementation

**Use Case Parameter:**
```csharp
public class SampleUseCase : IUseCaseParameter<string>
{
    public string Name { get; }

    public SampleUseCase(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}
```

**Use Case Implementation:**
```csharp
public class SampleUseCaseHandler : IUseCase<SampleUseCase, string>
{
    public async Task<ExecutionResult<string>> ExecuteAsync(SampleUseCase useCaseParameter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(useCaseParameter.Name))
        {
            return Execution.Failure<string>("Name cannot be empty or whitespace");
        }

        var greeting = $"Hello, {useCaseParameter.Name}! Welcome to FunctionalUseCases.";
        return Execution.Success(greeting);
    }
}
```

**Usage:**
```csharp
var dispatcher = serviceProvider.GetRequiredService<IUseCaseDispatcher>();
var useCaseParameter = new SampleUseCase("World");
var result = await dispatcher.ExecuteAsync(useCaseParameter);

if (result.ExecutionSucceeded)
    Console.WriteLine(result.CheckedValue); // "Hello, World! Welcome to FunctionalUseCases."
else
    Console.WriteLine(result.Error?.Message);
```

## Project Structure

```
FunctionalUseCases/
├── FunctionalUseCases.sln                    # Solution file
├── FunctionalUseCases/                       # Main library
│   ├── ExecutionResult.cs                   # Result types (generic & non-generic)
│   ├── Execution.cs                         # Factory methods
│   ├── ExecutionError.cs                    # Error types
│   ├── ExecutionException.cs                # Exception type
│   ├── UseCaseDispatcher.cs                 # Mediator implementation with execution behavior support
│   ├── PipelineBehaviorDelegate.cs           # Execution behavior delegate type
│   ├── Interfaces/                          # All interfaces
│   │   ├── IUseCase.cs                      # Use case parameter and implementation interfaces
│   │   ├── IUseCaseDispatcher.cs            # Dispatcher interface
│   │   └── IExecutionBehavior.cs            # Execution behavior interface
│   ├── Extensions/                          # Extension methods
│   │   ├── ExecutionResultExtensions.cs     # Logging & utility extensions
│   │   └── UseCaseRegistrationExtensions.cs # DI extensions (manual behavior registration required)
│   └── Sample/                              # Sample implementation
│       ├── SampleUseCase.cs                 # Example use case parameter
│       ├── SampleUseCaseHandler.cs          # Example use case implementation
│       └── LoggingBehavior.cs               # Example execution behavior
├── Sample/                                   # Console application
│   └── Program.cs                           # Demo application with execution behaviors
└── README.md                                # This file
```

## Building and Testing

```bash
# Build the solution
dotnet build

# Run the sample
cd Sample && dotnet run

# Run tests (if available)
dotnet test
```

**Sample Output with Execution Behaviors:**

```
=== FunctionalUseCases Sample Application with Execution Behaviors ===

Example 1: Successful execution
info: Starting execution of use case: SampleUseCase -> String
info: Successfully executed use case: SampleUseCase -> String in 103ms
✅ Success: Hello, World! Welcome to FunctionalUseCases.

Example 2: Failed execution (empty name)
info: Starting execution of use case: SampleUseCase -> String
warn: Use case execution failed: SampleUseCase -> String in 101ms. Error: Name cannot be empty or whitespace
❌ Error: Name cannot be empty or whitespace

Example 3: Use Case Chain
info: Starting execution of use case: SampleUseCase -> String
info: Successfully executed use case: SampleUseCase -> String in 98ms
info: Starting execution of use case: SampleUseCase -> String
info: Successfully executed use case: SampleUseCase -> String in 95ms
✅ Chain Success: Hello, SecondStep-9! Welcome to FunctionalUseCases.

Example 4: WithBehavior - Per-call transaction behavior
⚠️ Transaction behavior not available (expected): Unable to resolve service for type 'TransactionBehavior`2[SampleUseCase,String]'

Example 5: Use Case Chain with WithBehavior
⚠️ Chain with transaction behavior not available (expected): Unable to resolve service for type 'TransactionBehavior`2[SampleUseCase,String]'

Example 6: Interactive
Enter your name: Alice
info: Starting execution of use case: SampleUseCase -> String
info: Successfully executed use case: SampleUseCase -> String in 92ms
✅ Interactive Success: Hello, Alice! Welcome to FunctionalUseCases.
```

## Best Practices

1. **Keep Use Case Parameters Simple**: Each use case parameter should represent a single business operation's input data
2. **Immutable Use Case Parameters**: Make use case parameter properties read-only for thread safety
3. **Validation in Use Cases**: Perform validation in use case implementations, not in use case parameters
4. **Rich Error Handling**: Use ExecutionResult with specific error codes and appropriate log levels
5. **Async Operations**: Always use async/await for potentially long-running operations
6. **Cancellation Support**: Support cancellation tokens for responsive applications
7. **Meaningful Names**: Use descriptive names that clearly indicate the business operation being performed
8. **Single Responsibility**: Each use case should handle one specific business scenario
9. **Global vs Per-Call Behaviors**: Use global behaviors for cross-cutting concerns that apply everywhere (logging, monitoring). Use per-call behaviors for context-specific operations (transactions, validation, caching)
10. **Behavior Registration**: Remember to manually register both global and per-call execution behaviors as they are not automatically discovered
11. **Chain Design**: Design use case chains to be atomic units of work - if any step fails, the entire operation should be considered failed
12. **Result Passing**: Structure use case parameters to accept the exact data they need from previous use cases in chains
13. **Transaction Scope**: Use TransactionBehavior on chains rather than individual use cases when you need atomic operations across multiple steps
14. **Chain-Aware Behaviors**: Implement IScopedExecutionBehavior when creating behaviors that need to adapt based on execution context

## Interface Naming

The library uses clear, intent-revealing interface names:
- **IUseCaseParameter**: Represents the data/parameters for a use case
- **IUseCase**: Represents the actual use case implementation/logic
- **IExecutionBehavior**: Represents cross-cutting behavior that wraps use case execution
- **ExecuteAsync**: Method name that clearly indicates execution of business logic

This naming convention follows the principle that parameters define what data is needed, while use cases define how that data is processed, and behaviors define how execution is enhanced.

## Versioning

This library uses **semantic versioning** powered by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning):

- 🏷️ **Automatic Version Generation**: Versions are automatically generated based on Git history
- 📦 **NuGet Package Versioning**: Packages are versioned consistently across builds
- 🔍 **Runtime Version Access**: Version information is available at runtime via assembly attributes
- 🚀 **CI/CD Ready**: Integrates seamlessly with build pipelines

### Version Information Access

```csharp
// Access version information at runtime
var assembly = typeof(Execution).Assembly;
var version = assembly.GetName().Version;
var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

// Example output: "1.0.1+136a4d399f" (includes Git commit hash)
Console.WriteLine($"Library Version: {informationalVersion}");
```

## Dependencies

- **.NET 9.0** or later
- **Microsoft.Extensions.DependencyInjection** (9.0.8)
- **Microsoft.Extensions.Logging.Abstractions** (9.0.8) - For rich error handling and logging
- **Scrutor** (5.0.1) - For automatic service registration
- **Nerdbank.GitVersioning** (3.7.115) - For semantic versioning

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
