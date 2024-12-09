### Updated README.md for the `ExecutionEngine`

```markdown
# ExecutionEngine

A robust, scalable, and extensible task execution engine designed for Blazor Server applications. The `ExecutionEngine` enables the execution of complex workflows with support for dependencies, retries, conditional execution, compensation actions, progress reporting, and real-time updates using `MessagePipe`.

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Architecture](#architecture)
- [Getting Started](#getting-started)
  - [Installation](#installation)
  - [Configuration](#configuration)
  - [Usage](#usage)
- [Advanced Topics](#advanced-topics)
  - [Custom Filters](#custom-filters)
  - [Error Handling and Logging](#error-handling-and-logging)
- [Best Practices](#best-practices)
- [Contributing](#contributing)
- [License](#license)

---

## Overview

The `ExecutionEngine` simplifies the orchestration of complex workflows in Blazor Server applications. It provides powerful abstractions for defining tasks, handling dependencies, implementing retries, and managing progress updates with minimal effort.

---

## Features

- **Task Dependencies**: Define task dependencies to ensure proper execution order.
- **Retry Logic**: Customize retry counts and delays for resilient workflows.
- **Conditional Execution**: Dynamically control task execution with custom conditions.
- **Compensation Actions**: Handle rollback or compensation logic for failed tasks.
- **Progress Reporting**: Publish real-time updates using `MessagePipe`.
- **Parallel and Sequential Execution**: Execute tasks either sequentially or concurrently.
- **Cancellation Support**: Fully integrates with `CancellationToken` for responsive cancellation.
- **Extensibility**: Build custom filters and tasks for advanced use cases.
- **Thread Safety**: Designed with concurrency and thread safety as a priority.

---

## Architecture

### Core Components

#### ExecutionEngine
The `ExecutionEngine` is the core orchestrator that manages task execution. It handles:
- Dependency resolution and execution order.
- Retry policies and failure handling.
- Publishing progress updates and task statuses.
- Cancellation and resource cleanup.

#### Task Interface (`ITask`)
Defines the contract for tasks to be executed:
```csharp
public interface ITask
{
    string Name { get; }
    Func<ExecutionContext, bool>? Condition { get; set; }
    int MaxRetryCount { get; set; }
    TimeSpan RetryDelay { get; set; }
    bool ContinueOnFailure { get; set; }
    IReadOnlyList<string> Dependencies { get; }
    Func<ExecutionContext, CancellationToken, Task>? CompensationActionAsync { get; set; }
    bool Validate();
    Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken);
    void AddDependency(string dependency);
    void SetDependencies(IEnumerable<string> dependencies);
}
```

#### TaskBuilder
A fluent API for creating tasks:
```csharp
var task = TaskBuilder.Create("MyTask")
    .WithExecution(async (context, cancellationToken) =>
    {
        // Task logic
    })
    .WithMaxRetryCount(3)
    .WithRetryDelay(TimeSpan.FromSeconds(2))
    .Build();
```

#### Filters
`MessagePipe` filters such as logging, exception handling, and throttling provide cross-cutting concerns.

---

## Getting Started

### Installation

Add the required NuGet packages:
- `ExecutionEngine`
- `MessagePipe`

### Configuration

Register the services in your `Program.cs`:
```csharp
services.AddTaskExecutionEngine();
```

Service registration includes:
```csharp
public static IServiceCollection AddTaskExecutionEngine(this IServiceCollection services)
{
    services.AddOptions<ExecutionOptions>();
    services.AddTransient<ExecutionEngine>();
    services.AddSingleton<IExecutionEngineFactory, ExecutionEngineFactory>();

    // Register MessagePipe
    services.AddMessagePipe();
    services.AddSingleton<IAsyncPublisher<Guid, TaskProgressMessage>, AsyncPublisher<Guid, TaskProgressMessage>>();
    services.AddSingleton<IAsyncSubscriber<Guid, TaskProgressMessage>, AsyncSubscriber<Guid, TaskProgressMessage>>();

    // Additional services for filters and tasks
    return services;
}
```

---

### Usage

#### Creating Tasks

```csharp
var task = TaskBuilder.Create("Task1")
    .WithExecution(async (context, cancellationToken) =>
    {
        // Task logic here
    })
    .WithDependencies(new[] { "DependencyTask" })
    .WithCompensationAction(async (context, cancellationToken) =>
    {
        // Compensation logic here
    })
    .Build();
```

#### Executing Tasks
```csharp
var engine = ExecutionEngineFactory.CreateExecutionEngine(Guid.NewGuid());
engine.AddTask(task);
await engine.ExecuteAsync(CancellationToken.None);
```

#### Progress Updates
```csharp
private IDisposable _progressSubscription;

protected override void OnInitialized()
{
    _progressSubscription = ProgressSubscriber.Subscribe(_channelId, async (message, cancellationToken) =>
    {
        // Update UI or handle progress
    });
}

public void Dispose()
{
    _progressSubscription?.Dispose();
}
```

---

## Advanced Topics

### Custom Filters
Filters can add cross-cutting concerns like logging, throttling, or exception handling:
```csharp
public sealed class LoggingFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly ILogger _logger = LoggerFactory.Logger;

    public override async ValueTask HandleAsync(
        TMessage message,
        CancellationToken cancellationToken,
        Func<TMessage, CancellationToken, ValueTask> next)
    {
        _logger.Information("Handling message of type {MessageType}", typeof(TMessage).Name);
        await next(message, cancellationToken);
        _logger.Information("Finished handling message of type {MessageType}", typeof(TMessage).Name);
    }
}
```

### Parallel and Sequential Execution
Use `EnableParallelExecution` to control execution behavior:
- **Parallel**: Multiple tasks execute concurrently.
- **Sequential**: Tasks execute one at a time in defined order.

---

## Error Handling and Logging

- **Centralized Logging**: Utilize `ILogger` for consistent log output.
- **Filters**: Implement filters like `ExceptionHandlingFilter` for global exception handling.
- **Retry Logic**: Define retry policies for tasks to handle transient errors.

---

## Best Practices

- Use descriptive and unique names for tasks.
- Validate tasks with the `Validate` method.
- Honor `CancellationToken` in all task logic.
- Use `SetDependencies` to define execution order.
- Implement filters for advanced behaviors like logging or throttling.

---


## License

This project is licensed under the MIT License.
```
