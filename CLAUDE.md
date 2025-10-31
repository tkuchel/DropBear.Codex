# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DropBear.Codex is a modular collection of C# libraries built on .NET 9, providing robust, reusable components for enterprise-grade applications. The libraries use Railway-Oriented Programming principles with a comprehensive Result pattern for error handling.

**Repository**: https://github.com/tkuchel/DropBear.Codex
**Target Framework**: .NET 9.0
**Language**: C# 12+ (latest language features enabled)

## Build & Development Commands

### Building the Solution

```bash
# Build entire solution
dotnet build DropBear.Codex.sln

# Build specific project
dotnet build DropBear.Codex.Core/DropBear.Codex.Core.csproj

# Build in Release mode
dotnet build DropBear.Codex.sln -c Release

# Clean and rebuild
dotnet clean && dotnet build
```

### Package Management

```bash
# Restore NuGet packages
dotnet restore

# Create NuGet packages (automatically generated on build)
dotnet pack -c Release

# Publish specific project
dotnet publish DropBear.Codex.Core -c Release
```

### Code Quality

Projects enforce strict code quality standards:
- **Release builds**: Treat warnings as errors (`TreatWarningsAsErrors=true`)
- **Nullable warnings as errors**: CS8600, CS8602, CS8603, CS8625
- **Debug builds**: CS1591 (missing XML documentation) suppressed
- **Analyzers**: Meziantou.Analyzer and Roslynator.Analyzers enabled
- **Code analysis**: `AnalysisLevel=latest-all`, `AnalysisMode=AllEnabledByDefault`

## High-Level Architecture

### Dependency Structure

DropBear.Codex follows a **layered, acyclic architecture** where all projects depend on Core as the foundation:

```
Core (Foundation Layer)
 ├── Utilities
 ├── Serialization
 ├── Hashing
 ├── StateManagement
 ├── Tasks
 ├── Workflow
 ├── Notifications
 └── Files (depends on: Hashing, Serialization, Utilities)

Blazor (Presentation Layer)
 └── depends on: StateManagement, Workflow, Notifications
```

**Key Principle**: No circular dependencies. Core has zero internal dependencies.

### Core Projects

- **Core**: Result pattern, error handling, telemetry infrastructure
- **Utilities**: Extension methods and helper classes
- **Serialization**: Wrappers around JSON, MessagePack, and encrypted serialization
- **Hashing**: Various hashing implementations
- **StateManagement**: State machine implementation using Stateless library with snapshot management
- **Workflow**: Directed Acyclic Graph (DAG) workflow engine with compensation (Saga pattern)
- **Tasks**: Task/operation managers with retry and fallback support
- **Files**: Custom file format with serialization and verification
- **Notifications**: Notification infrastructure
- **Blazor**: Custom Blazor component library

## Result Pattern Architecture

The entire codebase uses Railway-Oriented Programming via the Result pattern from `DropBear.Codex.Core`.

### Result Types

#### `Result<TError>` - Operation with no return value
```csharp
public class Result<TError> : ResultBase, IResult<TError> where TError : ResultError
{
    // State: Success, Failure, Warning, PartialSuccess, Cancelled, Pending, NoOp
    ResultState State { get; }
    TError? Error { get; }
    bool IsSuccess { get; }
}
```

#### `Result<T, TError>` - Operation with return value
```csharp
public class Result<T, TError> : Result<TError> where TError : ResultError
{
    T Value { get; }
    T ValueOrDefault();
    T ValueOrThrow();
}
```

### ResultError Hierarchy

**Base**: `ResultError` (abstract record)
- **SimpleError**: Message-only errors
- **CodedError**: Errors with classification codes
- **OperationError**: Multi-field operation-specific errors

**Key Features**:
- Metadata as `FrozenDictionary<string, object>` (immutable, optimized)
- Severity: Low, Medium, High, Critical
- Category: General, Validation, Technical, Business
- Stack trace preservation
- Fluent configuration: `WithCode()`, `WithSeverity()`, `WithCategory()`, `WithMetadata()`

### Result Pattern Usage

```csharp
// Creating results
Result<SimpleError>.Success();
Result<SimpleError>.Failure(new SimpleError("Error message"));
Result<int, SimpleError>.Success(42);

// Chaining operations (monadic)
result
    .Map(value => value * 2)
    .Bind(value => SomeOperation(value))
    .Tap(value => Console.WriteLine(value))
    .Match(
        onSuccess: value => Console.WriteLine($"Success: {value}"),
        onFailure: error => Console.WriteLine($"Failed: {error.Message}")
    );

// Async operations
await result
    .MapAsync(async value => await GetDataAsync(value))
    .BindAsync(async value => await ProcessAsync(value))
    .MatchAsync(
        onSuccess: async value => await SaveAsync(value),
        onFailure: async error => await LogErrorAsync(error)
    );
```

**IMPORTANT**: Methods in this codebase return `Result<T>` instead of throwing exceptions. Always check `IsSuccess` or use `Match()` for error handling.

### Result Pattern Best Practices

**When writing code in this repository, follow these guidelines:**

#### ✅ DO

1. **Use Result<T, TError> for all operations that can fail**
   ```csharp
   public Result<User, UserError> GetUser(int id)
   {
       var user = _repository.Find(id);
       return user is not null
           ? Result<User, UserError>.Success(user)
           : Result<User, UserError>.Failure(UserError.NotFound(id));
   }
   ```

2. **Use factory methods for creating errors**
   ```csharp
   // Good - discoverable, typed
   return Result<User, UserError>.Failure(UserError.NotFound(userId));

   // Bad - stringly-typed
   return Result<User, UserError>.Failure(new UserError("User not found"));
   ```

3. **Use Unit for operations with no return value**
   ```csharp
   public async ValueTask<Result<Unit, SaveError>> SaveAsync(Data data)
   {
       await _repository.SaveAsync(data);
       return Result<Unit, SaveError>.Success(Unit.Value);
   }
   ```

4. **Use Match() for handling both success and failure**
   ```csharp
   result.Match(
       onSuccess: value => ProcessValue(value),
       onFailure: error => LogError(error)
   );
   ```

5. **Chain operations with Map/Bind for functional pipelines**
   ```csharp
   return await GetUserAsync(id)
       .MapAsync(async user => await EnrichUserAsync(user))
       .BindAsync(async user => await ValidateUserAsync(user))
       .MapAsync(async user => user.ToDto());
   ```

6. **Preserve exception context when creating Results**
   ```csharp
   try
   {
       return DoSomething();
   }
   catch (Exception ex)
   {
       return Result<T, MyError>.Failure(
           MyError.OperationFailed("Failed to do something"),
           ex  // Preserve the exception
       );
   }
   ```

7. **Add rich metadata to errors for debugging**
   ```csharp
   var error = UserError.NotFound(userId)
       .WithMetadata("Timestamp", DateTime.UtcNow)
       .WithMetadata("RequestId", requestId)
       .WithSeverity(ErrorSeverity.Medium);
   ```

#### ❌ DON'T

1. **Don't throw exceptions for expected errors**
   ```csharp
   // Bad
   public User GetUser(int id)
   {
       var user = _repository.Find(id);
       if (user is null)
           throw new NotFoundException($"User {id} not found");
       return user;
   }

   // Good
   public Result<User, UserError> GetUser(int id)
   {
       var user = _repository.Find(id);
       return user is not null
           ? Result<User, UserError>.Success(user)
           : Result<User, UserError>.Failure(UserError.NotFound(id));
   }
   ```

2. **Don't ignore IsSuccess without checking**
   ```csharp
   // Bad - could throw NullReferenceException
   var result = GetUser(id);
   var email = result.Value.Email;

   // Good
   var result = GetUser(id);
   if (result.IsSuccess)
   {
       var email = result.Value.Email;
   }
   ```

3. **Don't create Results with null values**
   ```csharp
   // Bad
   return Result<User, UserError>.Success(null);

   // Good - use a specific error
   return Result<User, UserError>.Failure(UserError.NotFound(id));
   ```

4. **Don't lose error context when converting types**
   ```csharp
   // Bad - loses original error details
   var userResult = _repository.GetUser(id);
   if (!userResult.IsSuccess)
       return Result<UserDto, ApiError>.Failure(new ApiError("Failed"));

   // Good - preserves context
   var userResult = _repository.GetUser(id);
   if (!userResult.IsSuccess)
   {
       return Result<UserDto, ApiError>.Failure(
           ApiError.FromRepositoryError(userResult.Error)
       );
   }
   ```

5. **Don't use ValueOrThrow() unless absolutely necessary**
   ```csharp
   // Bad - defeats the purpose of Result pattern
   var value = result.ValueOrThrow();

   // Good - handle both cases
   return result.IsSuccess
       ? ProcessValue(result.Value)
       : HandleError(result.Error);
   ```

#### Common Patterns

**Early Return Pattern:**
```csharp
public Result<OrderDto, OrderError> ProcessOrder(Order order)
{
    var validationResult = ValidateOrder(order);
    if (!validationResult.IsSuccess)
        return Result<OrderDto, OrderError>.Failure(/* map error */);

    var priceResult = CalculatePrice(order);
    if (!priceResult.IsSuccess)
        return Result<OrderDto, OrderError>.Failure(/* map error */);

    return Result<OrderDto, OrderError>.Success(order.ToDto());
}
```

**Fallback Pattern:**
```csharp
public async ValueTask<Config> GetConfigAsync()
{
    var primaryResult = await LoadPrimaryConfigAsync();
    if (primaryResult.IsSuccess)
        return primaryResult.Value;

    var backupResult = await LoadBackupConfigAsync();
    return backupResult.IsSuccess
        ? backupResult.Value
        : Config.Default;
}
```

**Retry Pattern:**
```csharp
public async ValueTask<Result<T, OperationError>> ExecuteWithRetryAsync<T>(
    Func<ValueTask<Result<T, OperationError>>> operation,
    int maxRetries = 3)
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        var result = await operation();
        if (result.IsSuccess)
            return result;

        if (attempt < maxRetries - 1)
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
    }

    return Result<T, OperationError>.Failure(
        OperationError.ForOperation("RetryExhausted", "All retries failed")
    );
}
```

### Project-Specific Error Types

Each project has its own error types that inherit from `ResultError`:

- **Notifications**: `NotificationError` - NotFound, OperationFailed, EncryptionFailed, etc.
- **StateManagement**: `SnapshotError`, `StateError`, `BuilderError`
- **Tasks**: `TaskExecutionError`, `TaskValidationError`, `CacheError`
- **Blazor**: `ComponentError`, `DataFetchError`, `JsInteropError`, `FileUploadError`
- **Serialization**: `SerializationError`, `DeserializationError`, `CompressionError`
- **Workflow**: `WorkflowExecutionError`, `WorkflowConfigurationError`, `WorkflowStepTimeoutError`
- **Files**: `FileOperationError`, `StorageError`, `BuilderError`
- **Hashing**: `HashingError`, `HashComputationError`, `HashVerificationError`

**Always use factory methods provided by these error types for consistency.**

## Workflow Architecture

The Workflow project implements a **DAG-based workflow engine** with compensation (Saga pattern).

### Key Concepts

**Workflow** = Directed Acyclic Graph of executable nodes
**Node Types**:
- `StepNode<TContext, TStep>`: Executes single workflow step
- `SequenceNode<TContext>`: Sequential execution of children
- `ParallelNode<TContext>`: Concurrent execution (`Task.WhenAll` semantics)
- `ConditionalNode<TContext>`: Branching based on predicate
- `DelayNode<TContext>`: Pause execution for specified duration

### Workflow Building Pattern

```csharp
var builder = new WorkflowBuilder<MyContext>("workflowId", "Workflow Name");
builder
    .StartWith<Step1>()
    .Then<Step2>()
    .If(ctx => ctx.Value > 10)
        .Then<StepIfTrue>()
        .Otherwise()
        .Then<StepIfFalse>()
    .Parallel(parallel => parallel
        .Add<ParallelStep1>()
        .Add<ParallelStep2>())
    .Delay(TimeSpan.FromSeconds(5))
    .Build();
```

### Workflow Step Implementation

**Base class approach** (recommended):
```csharp
public class MyStep : WorkflowStepBase<MyContext>
{
    public override string StepName => "My Step";
    public override bool CanRetry => true;
    public override TimeSpan? Timeout => TimeSpan.FromMinutes(5);

    public override async ValueTask<StepResult> ExecuteAsync(
        MyContext context,
        CancellationToken token)
    {
        try
        {
            // Step logic here
            return Success();
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    public override async ValueTask<StepResult> CompensateAsync(
        MyContext context,
        CancellationToken token)
    {
        // Rollback logic (Saga pattern)
        return Success();
    }
}
```

**Interface approach**:
```csharp
public class MyStep : IWorkflowStep<MyContext>
{
    public string StepName => "My Step";
    public bool CanRetry => true;
    public TimeSpan? Timeout => null;

    public async ValueTask<StepResult> ExecuteAsync(
        MyContext context,
        CancellationToken token) { /* ... */ }

    public async ValueTask<StepResult> CompensateAsync(
        MyContext context,
        CancellationToken token) { /* ... */ }
}
```

### Workflow Execution

```csharp
var engine = new WorkflowEngine(serviceProvider, logger);
var result = await engine.ExecuteAsync(workflowDefinition, context, cancellationToken);

// Result contains:
result.IsSuccess      // Overall success
result.Context        // Updated context
result.Metrics        // Execution metrics (time, step counts, retries)
result.ExecutionTrace // Optional detailed trace
result.Error          // Error if failed
```

### Real-Time Workflow Streaming

Stream workflow execution traces in real-time for monitoring, logging, or UI updates:

```csharp
var engine = new WorkflowEngine(serviceProvider, logger);

// Get both the trace stream and execution task
var (traceStream, executionTask) = engine.ExecuteWithStreamingAsync(
    workflowDefinition,
    context,
    cancellationToken: cancellationToken);

// Start consuming the stream immediately (runs in parallel with workflow)
var streamingTask = Task.Run(async () =>
{
    await foreach (var trace in traceStream.StreamAsync(cancellationToken))
    {
        // Process traces as they arrive
        Console.WriteLine($"Step: {trace.StepName} | Status: {trace.Status} | Duration: {trace.Duration}");

        // Send to monitoring system
        await telemetry.TrackStepAsync(trace);

        // Update UI
        await UpdateProgressBar(trace);
    }
});

// Wait for workflow to complete
var result = await executionTask;

// Wait for stream consumption to finish
await streamingTask;

// Check final result
if (result.IsSuccess)
{
    Console.WriteLine($"Workflow completed successfully in {result.Metrics.TotalDuration}");
}
```

**Key Benefits**:
- Zero-latency monitoring: See steps execute in real-time
- Memory-efficient: No need to store entire trace in memory
- Parallel processing: Stream consumption happens concurrently with execution
- Flexible consumers: Multiple consumers can read the same stream

**Common Use Cases**:
```csharp
// Real-time UI updates
await foreach (var trace in traceStream.StreamAsync(cancellationToken))
{
    await InvokeAsync(() =>
    {
        progressMessages.Add($"{trace.StepName}: {trace.Status}");
        StateHasChanged();
    });
}

// Live logging to external system
await foreach (var trace in traceStream.StreamAsync(cancellationToken))
{
    await logAggregator.SendAsync(new
    {
        WorkflowId = context.WorkflowId,
        StepName = trace.StepName,
        Status = trace.Status,
        Timestamp = trace.StartTime
    });
}

// Metrics collection
var stepDurations = new ConcurrentBag<TimeSpan>();
await foreach (var trace in traceStream.StreamAsync(cancellationToken))
{
    if (trace.Status == StepStatus.Completed)
    {
        stepDurations.Add(trace.Duration);
    }
}
```

### Compensation (Saga Pattern)

Enable compensation in execution options to automatically rollback on failure:
```csharp
var options = new WorkflowExecutionOptions
{
    EnableCompensation = true
};
```

When enabled, failed workflows execute `CompensateAsync()` on all previously successful steps **in reverse order (LIFO)**.

### Workflow Persistence

Workflows support persistence via `PersistentWorkflowEngine`:
- State stored in `IWorkflowStateRepository` (in-memory or custom implementation)
- Supports suspension and resumption with external signals
- Timeout handling via `WorkflowTimeoutService`
- Notifications via `IWorkflowNotificationService`

## StateManagement Architecture

The StateManagement project wraps the **Stateless** library with additional features.

### State Machine Building

```csharp
var builder = new StateMachineBuilder<MyState, MyTrigger>(MyState.Initial, logger);
builder
    .ConfigureState(MyState.Initial)
        .Permit(MyTrigger.Start, MyState.Running)
        .OnEntry(() => Console.WriteLine("Entered Initial"))
    .ConfigureState(MyState.Running)
        .Permit(MyTrigger.Complete, MyState.Completed)
        .Permit(MyTrigger.Fail, MyState.Failed)
        .OnExit(() => Console.WriteLine("Left Running"));

var stateMachine = builder.Build();
await stateMachine.FireAsync(MyTrigger.Start);
```

### Snapshot Management

```csharp
// Context must implement ICloneable<T>
public class MyContext : ICloneable<MyContext>
{
    public MyContext Clone() => new MyContext { /* deep copy */ };
}

// Create snapshot manager
var snapshotManager = new SimpleSnapshotManager<MyContext>(
    comparer: new MyComparer(),
    retentionPeriod: TimeSpan.FromHours(24),
    autoSaveInterval: TimeSpan.FromMinutes(5),
    logger: logger
);

// Usage
var result = snapshotManager.SaveState(context);
var restoreResult = snapshotManager.RestoreState(versionNumber);
var isDirtyResult = snapshotManager.IsDirty(currentContext);
```

**Features**:
- Automatic periodic snapshots (timer-based)
- Version tracking with incremental numbers
- Retention policy (auto-removes old snapshots)
- Thread-safe with `ConcurrentDictionary`

## Coding Conventions

### Asynchronous Code

- **Always use `ValueTask`** instead of `Task` for library methods
- Use `ConfigureAwait(false)` in library code (not UI code)
- Prefer `async`/`await` over `.Result` or `.Wait()`

```csharp
public async ValueTask<Result<int, SimpleError>> GetDataAsync(CancellationToken token)
{
    var data = await _repository.FetchAsync(token).ConfigureAwait(false);
    return Result<int, SimpleError>.Success(data);
}
```

### Immutability

- Use **record types** for data transfer objects
- Use **init-only properties** instead of setters
- Modifications create new instances via `with` expressions

```csharp
public sealed record WorkflowResult<TContext>
{
    public required TContext Context { get; init; }
    public required bool IsSuccess { get; init; }
    public WorkflowMetrics? Metrics { get; init; }
}

// Usage
var updated = original with { IsSuccess = true };
```

### Nullable Reference Types

- **Nullable is enabled** across all projects
- Use `?` for nullable types explicitly
- Use `!` null-forgiving operator sparingly (only when compiler analysis is incorrect)
- Validate nullability at API boundaries

### Dependency Injection

- Constructor injection is the standard pattern
- Steps resolved at runtime from `IServiceProvider`
- Use `IServiceProvider` parameter in workflow engine methods

```csharp
public class MyStep : WorkflowStepBase<MyContext>
{
    private readonly IMyService _service;

    public MyStep(IMyService service)
    {
        _service = service;
    }
}
```

### Logging

- Use **Serilog** for structured logging
- Use partial methods with LoggerMessage source generators for performance
- Include context (correlation IDs, workflow IDs, step names)

```csharp
[LoggerMessage(Level = LogLevel.Information, Message = "Step {StepName} completed")]
private partial void LogStepCompleted(string stepName);
```

### Error Handling

- **Never throw exceptions for expected errors** - use Result pattern
- Only throw for truly exceptional, unrecoverable situations
- Preserve exception context in ResultError via `SourceException`

```csharp
// Good
public Result<User, SimpleError> FindUser(int id)
{
    var user = _repository.Find(id);
    if (user is null)
        return Result<User, SimpleError>.Failure(new SimpleError("User not found"));
    return Result<User, SimpleError>.Success(user);
}

// Avoid
public User FindUser(int id)
{
    var user = _repository.Find(id);
    if (user is null)
        throw new InvalidOperationException("User not found");
    return user;
}
```

## Project-Specific Notes

### Working with Core

- Core is the foundation - understand the Result pattern first
- Use `FrozenDictionary` for read-optimized metadata collections
- Leverage telemetry integration (OpenTelemetry via Activities)
- Correlation IDs flow through all operations

### Working with Workflow

- Context types must be reference types (`where TContext : class`)
- Workflows lazily initialize node graphs (only built on first execution)
- Use `WorkflowBuilder` for fluent workflow construction
- Enable tracing in `WorkflowExecutionOptions` for debugging
- Test compensation logic thoroughly (LIFO execution order)

### Working with StateManagement

- State machines are lazily initialized via `Lazy<T>`
- Cannot reconfigure after calling `Build()`
- Snapshot contexts must implement `ICloneable<T>` with deep copy semantics
- Use `IStateComparer<T>` for custom dirty-checking logic

### Working with Serialization

- Wrappers abstract MessagePack, JSON, and encrypted serialization
- Use encrypted serialization for sensitive data (ProtectedData API)
- All serializers return `Result<byte[]>` or `Result<T>`

#### Streaming Deserialization

For large JSON arrays, use streaming deserialization to process elements without loading the entire array into memory:

```csharp
// Setup (in DI configuration)
services.AddJsonStreamingDeserializer();

// Usage - Stream large JSON array
public async Task ProcessLargeDataset(Stream jsonStream, IStreamingSerializer streamingSerializer)
{
    var processedCount = 0;
    var errors = new List<string>();

    await foreach (var result in streamingSerializer.DeserializeAsyncEnumerable<DataRecord>(
        jsonStream,
        cancellationToken))
    {
        if (result.IsSuccess)
        {
            // Process each record as it arrives
            await ProcessRecord(result.Value);
            processedCount++;

            if (processedCount % 1000 == 0)
            {
                _logger.Information("Processed {Count} records", processedCount);
            }
        }
        else
        {
            errors.Add(result.Error.Message);
        }
    }

    _logger.Information("Completed processing {Count} records with {ErrorCount} errors",
        processedCount, errors.Count);
}
```

**Key Benefits**:
- Memory-efficient: Only one element in memory at a time
- Fast time-to-first-element: Start processing before entire array loads
- Resilient: Errors don't stop the stream, each element is wrapped in Result
- Cancellable: Cancel mid-stream without reading remaining data

**Common Use Cases**:
```csharp
// Data import with progress tracking
var totalRecords = 0;
await foreach (var result in streamingSerializer.DeserializeAsyncEnumerable<Customer>(stream, cancellationToken))
{
    if (result.IsSuccess)
    {
        await database.InsertAsync(result.Value);
        totalRecords++;
        await progressTracker.UpdateAsync(totalRecords);
    }
}

// Filtering and transformation
await foreach (var result in streamingSerializer.DeserializeAsyncEnumerable<Transaction>(stream, cancellationToken))
{
    if (result.IsSuccess && result.Value.Amount > 1000)
    {
        await SendAlertAsync(result.Value);
    }
}

// Batched processing
var batch = new List<Product>();
await foreach (var result in streamingSerializer.DeserializeAsyncEnumerable<Product>(stream, cancellationToken))
{
    if (result.IsSuccess)
    {
        batch.Add(result.Value);

        if (batch.Count >= 100)
        {
            await ProcessBatchAsync(batch);
            batch.Clear();
        }
    }
}

if (batch.Count > 0)
{
    await ProcessBatchAsync(batch); // Process remaining items
}
```

### Working with Files

- Custom file format with embedded verification hashes
- Depends on Serialization, Hashing, Utilities projects
- Integrates with Azure Blob Storage via FluentStorage

## Common Patterns

### Generic Constraints

Many types use generic constraints extensively:

```csharp
// Workflow steps
public interface IWorkflowStep<TContext> where TContext : class

// State snapshots
public interface ICloneable<T> where T : ICloneable<T>

// Results
public class Result<TError> where TError : ResultError
```

### Performance Optimization

- `FrozenDictionary` for read-heavy scenarios
- `ValueTask` to reduce allocations
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for hot paths
- Object pooling (`Microsoft.Extensions.ObjectPool`)

### Observability

- Activities (OpenTelemetry) for distributed tracing
- Correlation IDs propagate through execution chains
- Metrics tracking built into workflow execution
- Structured logging with context enrichment

## Package Publication

Projects are configured for NuGet package generation:
- **GeneratePackageOnBuild**: `true` (auto-generates on build)
- **Version format**: `YYYY.MM.patch` (e.g., 2025.10.0)
- **SourceLink enabled**: Embedded source for debugging
- **Package validation enabled**: Ensures compatibility
- **Symbol packages**: `.snupkg` format for debugging
