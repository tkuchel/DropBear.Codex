# DropBear.Codex.Workflow Documentation

![Build Status](https://img.shields.io/badge/tests-17%2F17%20passing-brightgreen)
![Coverage](https://img.shields.io/badge/coverage-100%25-brightgreen)
![Version](https://img.shields.io/badge/version-1.0.0-blue)

## 📋 Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [Basic Usage](#basic-usage)
- [Advanced Features](#advanced-features)
- [Configuration](#configuration)
- [Best Practices](#best-practices)
- [API Reference](#api-reference)
- [Examples](#examples)
- [Troubleshooting](#troubleshooting)

## 🌟 Overview

DropBear.Codex.Workflow is a comprehensive, production-ready workflow engine for .NET applications. It provides a flexible, fluent API for defining and executing complex business processes with support for persistence, human approvals, external signals, parallel execution, and robust error handling.

### Key Benefits

- **🚀 High Performance**: Optimized execution engine with configurable retry policies
- **🔄 Persistent Workflows**: Long-running processes with state management
- **👥 Human-in-the-Loop**: Built-in approval and signal-based workflows
- **⚡ Parallel Execution**: Concurrent step processing with synchronization
- **📊 Rich Monitoring**: Comprehensive metrics, tracing, and logging
- **🛡️ Robust Error Handling**: Automatic retries, timeouts, and compensation patterns
- **🔌 Extensible**: Plugin architecture with dependency injection support

## ✨ Features

### Core Workflow Patterns
- ✅ **Sequential Workflows** - Step-by-step execution
- ✅ **Conditional Branching** - If/then/else logic with custom predicates
- ✅ **Parallel Execution** - Concurrent step processing
- ✅ **Loops & Iterations** - Repeatable workflow segments

### Advanced Capabilities
- ✅ **Persistent Workflows** - Long-running processes with state persistence
- ✅ **Signal-Based Workflows** - External event-driven execution
- ✅ **Approval Workflows** - Human approval processes with timeouts
- ✅ **Compensation Patterns** - Rollback and error recovery
- ✅ **Retry Policies** - Configurable retry strategies with exponential backoff

### Enterprise Features
- ✅ **Dependency Injection** - Full DI container support
- ✅ **Metrics & Tracing** - Performance monitoring and execution tracking
- ✅ **Timeout Management** - Step and workflow-level timeouts
- ✅ **Structured Logging** - Comprehensive logging with correlation IDs
- ✅ **Extension Methods** - Rich API extensions for common scenarios

## 🚀 Quick Start

### 1. Installation

```xml
<!-- Add to your .csproj file -->
<PackageReference Include="DropBear.Codex.Workflow" Version="1.0.0" />
```

### 2. Basic Setup

```csharp
using DropBear.Codex.Workflow.Extensions;
using Microsoft.Extensions.DependencyInjection;

// Configure services
var services = new ServiceCollection();
services.AddLogging();
services.AddWorkflowEngine();

// Register your workflows and steps
services.AddWorkflow<MyWorkflow, MyContext>();
services.AddWorkflowStep<Step1, MyContext>();
services.AddWorkflowStep<Step2, MyContext>();

var serviceProvider = services.BuildServiceProvider();
```

### 3. Define Your First Workflow

```csharp
public class MyContext
{
    public string Name { get; set; } = "";
    public List<string> CompletedSteps { get; set; } = new();
    public bool IsCompleted { get; set; }
}

public class MyWorkflow : Workflow<MyContext>
{
    public override string WorkflowId => "my-first-workflow";
    public override string DisplayName => "My First Workflow";

    protected override void Configure(WorkflowBuilder<MyContext> builder)
    {
        builder
            .StartWith<Step1>()
            .Then<Step2>()
            .If(ctx => ctx.Name.Contains("special"))
                .ThenExecute<SpecialStep>()
            .EndIf();
    }
}
```

### 4. Execute the Workflow

```csharp
var engine = serviceProvider.GetRequiredService<IWorkflowEngine>();
var workflow = serviceProvider.GetRequiredService<MyWorkflow>();
var context = new MyContext { Name = "Test Process" };

var result = await engine.ExecuteAsync(workflow, context);

if (result.IsSuccess)
{
    Console.WriteLine("Workflow completed successfully!");
    Console.WriteLine($"Execution time: {result.GetExecutionTime()}");
}
else
{
    Console.WriteLine($"Workflow failed: {result.ErrorMessage}");
}
```

## 🧠 Core Concepts

### Workflows
A **Workflow** defines the structure and execution logic of a business process. Workflows are composed of steps and control flow elements.

```csharp
public class DocumentApprovalWorkflow : Workflow<DocumentContext>
{
    public override string WorkflowId => "document-approval-v1";
    public override string DisplayName => "Document Approval Process";
    public override TimeSpan? WorkflowTimeout => TimeSpan.FromDays(7);

    protected override void Configure(WorkflowBuilder<DocumentContext> builder)
    {
        builder
            .StartWith<ValidateDocumentStep>()
            .Then<RequestApprovalStep>()
            .If(ctx => ctx.IsApproved)
                .ThenExecute<PublishDocumentStep>()
            .ElseExecute<RejectDocumentStep>()
            .EndIf();
    }
}
```

### Steps
A **Step** represents a single unit of work within a workflow. Steps are atomic operations that can succeed, fail, or require external input.

```csharp
public class ValidateDocumentStep : WorkflowStepBase<DocumentContext>
{
    public override string StepName => "ValidateDocument";
    public override bool CanRetry => true;
    public override TimeSpan? Timeout => TimeSpan.FromMinutes(5);

    public override async ValueTask<StepResult> ExecuteAsync(
        DocumentContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform validation logic
            var isValid = await ValidateDocument(context.DocumentId);
            
            if (isValid)
            {
                context.ValidationStatus = "Passed";
                return Success(new Dictionary<string, object>
                {
                    ["ValidationTime"] = DateTimeOffset.UtcNow,
                    ["ValidatedBy"] = "System"
                });
            }
            
            return Failure("Document validation failed", shouldRetry: false);
        }
        catch (Exception ex)
        {
            return Failure(ex, shouldRetry: true);
        }
    }
}
```

### Context
The **Context** is a shared data container that flows through all steps in a workflow, maintaining state and carrying information between steps.

```csharp
public class DocumentContext
{
    public required string DocumentId { get; init; }
    public required string DocumentTitle { get; init; }
    public required string AuthorId { get; init; }
    public string? ValidationStatus { get; set; }
    public bool IsApproved { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
}
```

## 📚 Basic Usage

### Sequential Workflows

The most common pattern - steps execute one after another:

```csharp
protected override void Configure(WorkflowBuilder<MyContext> builder)
{
    builder
        .StartWith<InitializeStep>()
        .Then<ProcessDataStep>()
        .Then<ValidateResultStep>()
        .Then<FinalizeStep>();
}
```

### Conditional Workflows

Branch execution based on context state:

```csharp
protected override void Configure(WorkflowBuilder<OrderContext> builder)
{
    builder
        .StartWith<ValidateOrderStep>()
        .If(ctx => ctx.OrderAmount > 1000)
            .ThenExecute<RequireManagerApprovalStep>()
        .ElseExecute<AutoApproveStep>()
        .EndIf()
        .Then<ProcessPaymentStep>();
}
```

### Parallel Workflows

Execute multiple steps concurrently:

```csharp
protected override void Configure(WorkflowBuilder<ProcessingContext> builder)
{
    builder
        .StartWith<PrepareDataStep>()
        .InParallel()
            .Execute<ProcessImageStep>()
            .Execute<ProcessTextStep>()
            .Execute<ProcessMetadataStep>()
        .EndParallel()
        .Then<CombineResultsStep>();
}
```

### Delay Steps

Add timing delays to workflows:

```csharp
protected override void Configure(WorkflowBuilder<NotificationContext> builder)
{
    builder
        .StartWith<SendInitialNotificationStep>()
        .Delay(TimeSpan.FromHours(1))
        .Then<SendReminderStep>()
        .Delay(TimeSpan.FromDays(1))
        .Then<SendFinalNoticeStep>();
}
```

## 🔥 Advanced Features

### Persistent Workflows

For long-running processes that need to survive application restarts:

```csharp
// Setup persistent workflows
services.AddPersistentWorkflowEngine();
services.AddWorkflowStateRepository<SqlWorkflowStateRepository>();
services.AddWorkflowNotificationService<EmailNotificationService>();

// Start a persistent workflow
var persistentEngine = serviceProvider.GetRequiredService<IPersistentWorkflowEngine>();
var result = await persistentEngine.StartPersistentWorkflowAsync(workflow, context);

Console.WriteLine($"Workflow started with ID: {result.WorkflowInstanceId}");

// Resume a workflow later
var resumeResult = await persistentEngine.ResumeWorkflowAsync<MyContext>(workflowInstanceId);
```

### Signal-Based Workflows

Wait for external events during execution:

```csharp
public class UserRegistrationStep : WaitForSignalStep<UserContext, EmailVerification>
{
    public override string SignalName => "email_verification";
    public override TimeSpan? SignalTimeout => TimeSpan.FromHours(24);

    public override ValueTask<StepResult> ProcessSignalAsync(
        UserContext context, 
        EmailVerification? signalData, 
        CancellationToken cancellationToken = default)
    {
        if (signalData?.IsVerified == true)
        {
            context.EmailVerified = true;
            context.VerificationToken = signalData.Token;
            return ValueTask.FromResult(Success());
        }
        
        return ValueTask.FromResult(Failure("Email verification failed"));
    }
}

// Send signal to workflow
await persistentEngine.SignalWorkflowAsync(
    workflowInstanceId, 
    "email_verification", 
    new EmailVerification { IsVerified = true, Token = "abc123" });
```

### Approval Workflows

Built-in human approval processes:

```csharp
public class DocumentApprovalStep : WaitForApprovalStep<DocumentContext>
{
    public override ApprovalRequest CreateApprovalRequest(DocumentContext context)
    {
        return new ApprovalRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            Title = $"Document Approval: {context.DocumentTitle}",
            Description = $"Please review and approve document {context.DocumentId}",
            RequestedBy = context.AuthorId,
            RequestedAt = DateTimeOffset.UtcNow,
            ApproverEmails = context.ApproverIds.Select(id => $"{id}@company.com").ToList(),
            Timeout = TimeSpan.FromDays(3)
        };
    }

    protected override ValueTask OnApprovalReceivedAsync(
        DocumentContext context, 
        ApprovalResponse approvalResponse, 
        CancellationToken cancellationToken)
    {
        context.IsApproved = approvalResponse.IsApproved;
        context.ApprovedBy = approvalResponse.ApprovedBy;
        context.ApprovalComments = approvalResponse.Comments;
        
        return ValueTask.CompletedTask;
    }
}
```

### Compensation Patterns

Implement rollback logic for failed workflows:

```csharp
public class CreateUserAccountStep : WorkflowStepBase<UserRegistrationContext>
{
    public override async ValueTask<StepResult> ExecuteAsync(
        UserRegistrationContext context, 
        CancellationToken cancellationToken = default)
    {
        var userId = await _userService.CreateUserAsync(context.UserData);
        context.CreatedUserId = userId;
        
        return Success();
    }

    // Compensation logic - called if workflow fails later
    public override async ValueTask<StepResult> CompensateAsync(
        UserRegistrationContext context, 
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(context.CreatedUserId))
        {
            await _userService.DeleteUserAsync(context.CreatedUserId);
        }
        
        return Success();
    }
}
```

## ⚙️ Configuration

### Workflow Execution Options

```csharp
var options = new WorkflowExecutionOptions
{
    MaxRetryAttempts = 3,
    RetryBaseDelay = TimeSpan.FromMilliseconds(100),
    MaxRetryDelay = TimeSpan.FromMinutes(1),
    EnableExecutionTracing = true,
    EnableMemoryMetrics = true,
    MaxDegreeOfParallelism = Environment.ProcessorCount
};

var result = await engine.ExecuteAsync(workflow, context, options);
```

### Retry Policies

```csharp
public class RetryPolicy
{
    public static RetryPolicy Aggressive => new()
    {
        MaxAttempts = 5,
        BaseDelay = TimeSpan.FromMilliseconds(50),
        MaxDelay = TimeSpan.FromSeconds(30),
        BackoffMultiplier = 2.0,
        ShouldRetryPredicate = ex => ex is not SecurityException
    };
}
```

### Dependency Injection Setup

```csharp
services.AddWorkflowEngine()
    .ConfigureWorkflowOptions(options =>
    {
        options.MaxRetryAttempts = 5;
        options.EnableExecutionTracing = true;
        options.EnableMemoryMetrics = true;
    });

// Auto-register workflows from assembly
services.AddWorkflowStepsFromAssembly(typeof(MyWorkflow).Assembly);
```

## 🎯 Best Practices

### 1. Context Design
- Keep contexts lightweight and serializable
- Use immutable properties where possible
- Avoid storing large objects or dependencies

```csharp
// ✅ Good
public class OrderContext
{
    public required string OrderId { get; init; }
    public required decimal Amount { get; init; }
    public OrderStatus Status { get; set; }
    public List<string> ProcessingNotes { get; set; } = new();
}

// ❌ Avoid
public class BadContext
{
    public IDbContext Database { get; set; } // Don't store dependencies
    public byte[] LargeFile { get; set; }    // Don't store large data
}
```

### 2. Step Design
- Make steps idempotent when possible
- Use meaningful step names
- Implement proper error handling
- Add appropriate timeouts

```csharp
public class ProcessPaymentStep : WorkflowStepBase<OrderContext>
{
    public override string StepName => "ProcessPayment";
    public override bool CanRetry => true;
    public override TimeSpan? Timeout => TimeSpan.FromMinutes(2);

    public override async ValueTask<StepResult> ExecuteAsync(
        OrderContext context, 
        CancellationToken cancellationToken = default)
    {
        // Check if already processed (idempotent)
        if (context.PaymentStatus == PaymentStatus.Completed)
        {
            return Success();
        }

        try
        {
            var result = await _paymentService.ProcessPaymentAsync(
                context.OrderId, 
                context.Amount, 
                cancellationToken);

            context.PaymentStatus = PaymentStatus.Completed;
            context.TransactionId = result.TransactionId;

            return Success(new Dictionary<string, object>
            {
                ["TransactionId"] = result.TransactionId,
                ["ProcessedAt"] = DateTimeOffset.UtcNow
            });
        }
        catch (PaymentDeclinedException ex)
        {
            context.PaymentStatus = PaymentStatus.Declined;
            return Failure($"Payment declined: {ex.Reason}", shouldRetry: false);
        }
        catch (PaymentServiceUnavailableException ex)
        {
            return Failure($"Payment service unavailable: {ex.Message}", shouldRetry: true);
        }
    }
}
```

### 3. Error Handling
- Use specific exception types
- Implement appropriate retry logic
- Provide meaningful error messages
- Log errors with correlation IDs

```csharp
public class RobustStep : WorkflowStepBase<MyContext>
{
    private readonly ILogger<RobustStep> _logger;

    public override async ValueTask<StepResult> ExecuteAsync(
        MyContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await DoWorkAsync(context, cancellationToken);
            return Success();
        }
        catch (TransientException ex)
        {
            _logger.LogWarning(ex, "Transient error in {StepName}, will retry", StepName);
            return Failure($"Transient error: {ex.Message}", shouldRetry: true);
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogError(ex, "Business rule violation in {StepName}", StepName);
            return Failure($"Business rule violation: {ex.Message}", shouldRetry: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in {StepName}", StepName);
            return Failure($"Unexpected error: {ex.Message}", shouldRetry: false);
        }
    }
}
```

### 4. Performance Optimization
- Use parallel execution for independent steps
- Configure appropriate timeouts
- Monitor workflow metrics
- Optimize context serialization for persistent workflows

```csharp
// Monitor performance
var result = await engine.ExecuteAsync(workflow, context);
var metrics = result.GetPerformanceSummary();

Console.WriteLine($"Total time: {metrics["TotalExecutionTime"]}");
Console.WriteLine($"Steps executed: {metrics["StepsExecuted"]}");
Console.WriteLine($"Average step time: {metrics["AverageStepTime"]}");
```

## 📖 API Reference

### Core Interfaces

#### IWorkflowEngine
Main workflow execution engine.

```csharp
public interface IWorkflowEngine
{
    ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class;

    ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        WorkflowExecutionOptions options,
        CancellationToken cancellationToken = default) where TContext : class;
}
```

#### IPersistentWorkflowEngine
Extended engine for long-running workflows.

```csharp
public interface IPersistentWorkflowEngine : IWorkflowEngine
{
    ValueTask<PersistentWorkflowResult<TContext>> StartPersistentWorkflowAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class;

    ValueTask<PersistentWorkflowResult<TContext>> ResumeWorkflowAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class;

    ValueTask<bool> SignalWorkflowAsync<TData>(
        string workflowInstanceId,
        string signalName,
        TData? signalData = default,
        CancellationToken cancellationToken = default);
}
```

### Extension Methods

```csharp
// Execute with factory
var result = await engine.ExecuteAsync(workflow, () => new MyContext());

// Try execute (returns bool)
var success = await engine.TryExecuteAsync(workflow, context);

// Result extensions
var executionTime = result.GetExecutionTime();
var failedSteps = result.GetFailedSteps();
var performanceSummary = result.GetPerformanceSummary();
```

## 💡 Examples

### E-commerce Order Processing

```csharp
public class OrderProcessingWorkflow : Workflow<OrderContext>
{
    public override string WorkflowId => "order-processing-v2";
    public override string DisplayName => "E-commerce Order Processing";

    protected override void Configure(WorkflowBuilder<OrderContext> builder)
    {
        builder
            .StartWith<ValidateOrderStep>()
            .Then<CheckInventoryStep>()
            .If(ctx => ctx.RequiresApproval)
                .ThenExecute<RequestManagerApprovalStep>()
            .EndIf()
            .InParallel()
                .Execute<ProcessPaymentStep>()
                .Execute<ReserveInventoryStep>()
                .Execute<GenerateShippingLabelStep>()
            .EndParallel()
            .Then<FulfillOrderStep>()
            .Then<SendConfirmationEmailStep>();
    }
}
```

### User Registration with Email Verification

```csharp
public class UserRegistrationWorkflow : Workflow<UserRegistrationContext>
{
    public override string WorkflowId => "user-registration-v1";
    public override string DisplayName => "User Registration";
    public override TimeSpan? WorkflowTimeout => TimeSpan.FromHours(48);

    protected override void Configure(WorkflowBuilder<UserRegistrationContext> builder)
    {
        builder
            .StartWith<ValidateUserDataStep>()
            .Then<CreateUserAccountStep>()
            .Then<SendVerificationEmailStep>()
            .Then<WaitForEmailVerificationStep>()
            .If(ctx => ctx.EmailVerified)
                .ThenExecute<ActivateUserAccountStep>()
                .Then<SendWelcomeEmailStep>()
            .ElseExecute<DeactivateUserAccountStep>()
            .EndIf();
    }
}
```

### Document Approval Process

```csharp
public class DocumentApprovalWorkflow : Workflow<DocumentApprovalContext>
{
    public override string WorkflowId => "document-approval-v1";
    public override string DisplayName => "Document Approval Workflow";
    public override TimeSpan? WorkflowTimeout => TimeSpan.FromDays(7);

    protected override void Configure(WorkflowBuilder<DocumentApprovalContext> builder)
    {
        builder
            .StartWith<RequestDocumentApprovalStep>()
            .If(ctx => ctx.IsApproved)
                .ThenExecute<PublishDocumentStep>()
                .Then<NotifyStakeholdersStep>()
            .ElseExecute<ArchiveDocumentStep>()
            .EndIf();
    }
}
```

## 🔧 Troubleshooting

### Common Issues

#### 1. Workflow Not Starting
```csharp
// Check service registration
services.AddWorkflow<MyWorkflow, MyContext>();
services.AddWorkflowStep<MyStep, MyContext>();

// Verify workflow ID is unique
public override string WorkflowId => "unique-workflow-id";
```

#### 2. Steps Not Executing
```csharp
// Ensure proper step chaining
builder
    .StartWith<Step1>()  // ✅ Starts the workflow
    .Then<Step2>()       // ✅ Links to next step
    .Then<Step3>();      // ✅ Links to final step

// Check step implementation
public override async ValueTask<StepResult> ExecuteAsync(...)
{
    // Must return Success() or Failure()
    return Success();
}
```

#### 3. Persistent Workflows Not Resuming
```csharp
// Verify state repository registration
services.AddWorkflowStateRepository<YourStateRepository>();

// Check workflow reconstruction
services.AddWorkflow<MyWorkflow, MyContext>(ServiceLifetime.Singleton);
```

#### 4. Signals Not Working
```csharp
// Ensure signal names match
public override string SignalName => "user_approval";

// Send signal with exact same name
await engine.SignalWorkflowAsync(instanceId, "user_approval", data);
```

### Debugging Tips

#### Enable Detailed Logging
```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});
```

#### Use Execution Tracing
```csharp
var options = new WorkflowExecutionOptions
{
    EnableExecutionTracing = true
};

var result = await engine.ExecuteAsync(workflow, context, options);

// Examine trace
foreach (var trace in result.ExecutionTrace)
{
    Console.WriteLine($"{trace.StepName}: {trace.Result.IsSuccess}");
    if (!trace.Result.IsSuccess)
    {
        Console.WriteLine($"  Error: {trace.Result.ErrorMessage}");
    }
}
```

#### Monitor Performance
```csharp
var performanceInfo = result.GetPerformanceSummary();
var longestStep = result.GetLongestRunningStep();
var failedSteps = result.GetFailedSteps();
```

### Support

For additional support and advanced scenarios:
- 📧 Email: support@dropbear.dev
- 📖 Documentation: https://docs.dropbear.dev/workflow
- 🐛 Issues: https://github.com/dropbear-software/workflow/issues
- 💬 Discussions: https://github.com/dropbear-software/workflow/discussions

---

*Last updated: August 22, 2025*
*Version: 1.0.0*