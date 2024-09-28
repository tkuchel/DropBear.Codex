# ExecutionEngine

A robust and flexible task execution engine designed for Blazor Server applications. The ExecutionEngine facilitates the
execution of complex tasks with support for dependencies, retry logic, conditional execution, compensation actions, and
progress reporting through the MessagePipe library.

---

## Table of Contents

- Overview - Features - Architecture - ExecutionEngine - ITask Interface - SimpleTask and TaskBuilder -
  ExecutionContext - MessagePipe Integration - Getting Started - Installation - Configuration - Usage - Advanced
  Topics - Custom Filters - Error Handling and Logging - Best Practices - Contributing - License

---

## Overview

The ExecutionEngine is a component designed to manage and execute a series of tasks within a Blazor Server application.
It handles task execution flow, dependencies, retries, and progress updates, allowing developers to focus on
implementing the business logic of individual tasks.

---

## Features

- Task Dependencies: Define tasks that depend on the completion of other tasks. - Retry Logic: Configure maximum retry
  counts and delays between retries. - Conditional Execution: Execute tasks based on custom conditions. - Compensation
  Actions: Define actions to roll back or compensate for failed tasks. - Progress Reporting: Receive real-time progress
  updates through MessagePipe. - Extensibility: Easily extend functionality with custom tasks and filters. - Thread
  Safety: Designed with concurrency and thread safety in mind.

---

## Architecture

### ExecutionEngine

The ExecutionEngine is the core component responsible for orchestrating the execution of tasks. It manages the task
queue, handles dependencies, and publishes progress updates.

#### Key Responsibilities:

- Managing task execution order based on dependencies. - Handling retries and failure policies. - Publishing progress
  and status messages. - Providing an ExecutionContext to tasks for shared resources.

### ITask Interface

Defines the contract for tasks that can be executed within the ExecutionEngine.

```csharp 
public interface ITask { string Name { get; } Func<ExecutionContext, bool>? Condition { get; set; } int
MaxRetryCount { get; set; } TimeSpan RetryDelay { get; set; } bool ContinueOnFailure { get; set; } IReadOnlyList<string>
Dependencies { get; } Func<ExecutionContext, Task>? CompensationActionAsync { get; set; } bool Validate(); Task
ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken); void Execute(ExecutionContext context);
void AddDependency(string dependency); void SetDependencies(IEnumerable<string> dependencies); }
```

### SimpleTask and TaskBuilder

SimpleTask is a concrete implementation of ITask for straightforward scenarios. It allows you to define execution logic
using delegates.

TaskBuilder provides a fluent API to create and configure SimpleTask instances easily.

#### Example:

```csharp
 var task = TaskBuilder.Create("MyTask")  .WithExecution(async (context, cancellationToken) =>  { // Task logic
here })  .WithMaxRetryCount(3)  .Build();
```

### ExecutionContext

Provides shared data and services to tasks during execution. It includes:

- Logger: For logging within tasks. - CancellationToken: To support task cancellation. - Shared Data: A dictionary for
  passing data between tasks.

### MessagePipe Integration

ExecutionEngine uses MessagePipe for publishing progress updates and task status messages. This allows for decoupled
communication and real-time UI updates.

#### Messages:

- TaskProgressMessage - TaskStartedMessage - TaskCompletedMessage - TaskFailedMessage

---

## Getting Started

### Installation

Add the necessary packages to your Blazor Server application:

- ExecutionEngine Package: NuGet Package - MessagePipe: NuGet Package

### Configuration

Register the required services in your Startup.cs or Program.cs:

```csharp 
services.AddTaskExecutionEngine();
```

#### Service Registration Extension Method:

```csharp 
public static IServiceCollection AddTaskExecutionEngine(this IServiceCollection services) {
services.AddOptions<ExecutionOptions>(); services.AddTransient<ExecutionEngine>(); services.AddSingleton<
IExecutionEngineFactory, ExecutionEngineFactory>(); services.AddMessagePipe(options =>  {
options.EnableCaptureStackTrace = false; }); // Register MessagePipe publishers and subscribers services.AddSingleton<
IAsyncPublisher<Guid, TaskProgressMessage>, AsyncPublisher<Guid, TaskProgressMessage>>(); services.AddSingleton<
IAsyncSubscriber<Guid, TaskProgressMessage>, AsyncSubscriber<Guid, TaskProgressMessage>>(); // Register other necessary
services and filters return services; }
```

### Usage

#### Creating Tasks

Use TaskBuilder to create tasks:

```csharp 
var task1 = TaskBuilder.Create("Task1")  .WithExecution(async (context, cancellationToken) =>  { // Task
execution logic })  .Build();
```

#### Using ExecutionEngine

In your Blazor component:

```csharp 
inject IExecutionEngineFactory ExecutionEngineFactory code { private ExecutionEngine _executionEngine; private
Guid _channelId; protected override async Task OnInitializedAsync()  { // Retrieve or generate your channel ID  _
channelId = /* Your logic to get channel ID */; // Create the ExecutionEngine instance  _executionEngine =
ExecutionEngineFactory.CreateExecutionEngine(_channelId); // Add tasks  _executionEngine.AddTask(task1); // Add more
tasks as needed // Execute tasks await _executionEngine.ExecuteAsync(CancellationToken.None); } }
```

#### Subscribing to Progress Updates

```csharp 
inject IAsyncSubscriber<Guid, TaskProgressMessage> ProgressSubscriber code { private IDisposable _
progressSubscription; protected override void OnInitialized()  {  _progressSubscription = ProgressSubscriber.Subscribe(_
channelId, async (message, cancellationToken) =>  { // Update UI or handle progress }); } public void Dispose()  {  _
progressSubscription?.Dispose(); } }
````

---

## Advanced Topics

### Custom Filters

Implement MessagePipe filters to enhance functionality, such as logging, exception handling, or performance monitoring.

#### Example: Logging Filter

```csharp 
public class LoggingFilter<TMessage> : AsyncMessageHandlerFilter<TMessage> {  private readonly ILogger<LoggingFilter<TMessage>> _logger;   public LoggingFilter(ILogger<LoggingFilter<TMessage>> logger)  {  _logger = logger;  }   public override async ValueTask HandleAsync(TMessage message, CancellationToken cancellationToken, Func<TMessage, CancellationToken, ValueTask> next)  {  _logger.LogInformation("Handling message of type {MessageType}", typeof(TMessage).Name);  await next(message, cancellationToken);  _logger.LogInformation("Finished handling message of type {MessageType}", typeof(TMessage).Name);  } } 
```

#### Registration

```csharp
 services.AddScoped(typeof(AsyncMessageHandlerFilter<>), typeof(LoggingFilter<>));
```

### Error Handling and Logging

- Centralized Logging: Use the provided ILogger in ExecutionContext and filters. - Exception Handling: Implement filters
  like ExceptionHandlingFilter to catch and log exceptions. - Validation: Ensure tasks validate their configuration
  using Validate() method.

---

## Best Practices

- Task Naming: Use unique and descriptive names for tasks. - Dependency Management: Clearly define task dependencies to
  avoid cyclic references. - Cancellation Tokens: Honor the CancellationToken in your task execution logic. - Thread
  Safety: Ensure that shared resources are accessed in a thread-safe manner. - Logging Levels: Use appropriate logging
  levels (Information, Warning, Error) for clarity.

---

## Contributing

Contributions are welcome! Please follow the guidelines:

- Fork the Repository: Create your branch. - Implement Features or Fix Bugs: Ensure code adheres to the project's
  style. - Write Tests: Maintain high test coverage. - Submit a Pull Request: Describe your changes and link to any
  relevant issues.

---

## License

This project is licensed under the MIT License.

---
