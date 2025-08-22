# DropBear.Codex.Workflow

![Build Status](https://img.shields.io/badge/tests-17%2F17%20passing-brightgreen)
![Coverage](https://img.shields.io/badge/coverage-100%25-brightgreen)
![Version](https://img.shields.io/badge/version-1.0.0-blue)
![License](https://img.shields.io/badge/license-MIT-blue)

A comprehensive, production-ready workflow engine for .NET applications with support for persistence, human approvals, external signals, parallel execution, and robust error handling.

## ✨ Features

- 🚀 **High Performance** - Optimized execution engine with configurable retry policies
- 🔄 **Persistent Workflows** - Long-running processes with state management
- 👥 **Human-in-the-Loop** - Built-in approval and signal-based workflows
- ⚡ **Parallel Execution** - Concurrent step processing with synchronization
- 📊 **Rich Monitoring** - Comprehensive metrics, tracing, and logging
- 🛡️ **Robust Error Handling** - Automatic retries, timeouts, and compensation patterns
- 🔌 **Extensible** - Plugin architecture with dependency injection support

## 🚀 Quick Start

### Installation

```bash
dotnet add package DropBear.Codex.Workflow
```

### Basic Example

```csharp
// 1. Define your context
public class OrderContext
{
    public string OrderId { get; set; } = "";
    public decimal Amount { get; set; }
    public bool IsApproved { get; set; }
    public OrderStatus Status { get; set; }
}

// 2. Create workflow steps
public class ValidateOrderStep : WorkflowStepBase<OrderContext>
{
    public override async ValueTask<StepResult> ExecuteAsync(
        OrderContext context, 
        CancellationToken cancellationToken = default)
    {
        // Validate the order
        if (context.Amount <= 0)
            return Failure("Invalid order amount");
            
        context.Status = OrderStatus.Validated;
        return Success();
    }
}

// 3. Define your workflow
public class OrderProcessingWorkflow : Workflow<OrderContext>
{
    public override string WorkflowId => "order-processing-v1";
    public override string DisplayName => "Order Processing";

    protected override void Configure(WorkflowBuilder<OrderContext> builder)
    {
        builder
            .StartWith<ValidateOrderStep>()
            .If(ctx => ctx.Amount > 1000)
                .ThenExecute<RequireApprovalStep>()
            .EndIf()
            .Then<ProcessPaymentStep>()
            .Then<FulfillOrderStep>();
    }
}

// 4. Execute the workflow
var services = new ServiceCollection();
services.AddWorkflowEngine();
services.AddWorkflow<OrderProcessingWorkflow, OrderContext>();
// Register your steps...

var serviceProvider = services.BuildServiceProvider();
var engine = serviceProvider.GetRequiredService<IWorkflowEngine>();
var workflow = serviceProvider.GetRequiredService<OrderProcessingWorkflow>();

var context = new OrderContext 
{ 
    OrderId = "ORD-001", 
    Amount = 1500 
};

var result = await engine.ExecuteAsync(workflow, context);

if (result.IsSuccess)
{
    Console.WriteLine("Order processed successfully!");
}
```

## 🏗️ Core Concepts

### Workflows
Define business processes using a fluent builder API:

```csharp
protected override void Configure(WorkflowBuilder<MyContext> builder)
{
    builder
        .StartWith<InitializeStep>()
        .If(ctx => ctx.RequiresApproval)
            .ThenExecute<ApprovalStep>()
        .EndIf()
        .InParallel()
            .Execute<ProcessDataStep>()
            .Execute<SendNotificationStep>()
        .EndParallel()
        .Then<FinalizeStep>();
}
```

### Steps
Implement individual units of work:

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
        try
        {
            var result = await _paymentService.ProcessAsync(context.OrderId);
            context.TransactionId = result.TransactionId;
            return Success();
        }
        catch (PaymentException ex)
        {
            return Failure($"Payment failed: {ex.Message}", shouldRetry: true);
        }
    }
}
```

## 🔥 Advanced Features

### Persistent Workflows
For long-running processes that survive application restarts:

```csharp
services.AddPersistentWorkflowEngine();
services.AddWorkflowStateRepository<SqlWorkflowStateRepository>();

var persistentEngine = serviceProvider.GetRequiredService<IPersistentWorkflowEngine>();
var result = await persistentEngine.StartPersistentWorkflowAsync(workflow, context);

// Resume later
await persistentEngine.ResumeWorkflowAsync<OrderContext>(result.WorkflowInstanceId);
```

### Signal-Based Workflows
Wait for external events:

```csharp
public class WaitForApprovalStep : WaitForSignalStep<OrderContext, ApprovalData>
{
    public override string SignalName => "order_approval";
    public override TimeSpan? SignalTimeout => TimeSpan.FromDays(3);

    public override ValueTask<StepResult> ProcessSignalAsync(
        OrderContext context, 
        ApprovalData? signalData, 
        CancellationToken cancellationToken = default)
    {
        context.IsApproved = signalData?.Approved ?? false;
        return ValueTask.FromResult(Success());
    }
}

// Send signal
await persistentEngine.SignalWorkflowAsync(
    workflowInstanceId, 
    "order_approval", 
    new ApprovalData { Approved = true });
```

### Human Approval Workflows
Built-in approval processes:

```csharp
public class DocumentApprovalStep : WaitForApprovalStep<DocumentContext>
{
    public override ApprovalRequest CreateApprovalRequest(DocumentContext context)
    {
        return new ApprovalRequest
        {
            Title = $"Document Approval: {context.DocumentTitle}",
            Description = $"Please review document {context.DocumentId}",
            ApproverEmails = new[] { "manager@company.com" },
            Timeout = TimeSpan.FromDays(7)
        };
    }
}
```

### Parallel Execution
Execute multiple steps concurrently:

```csharp
builder
    .StartWith<PrepareDataStep>()
    .InParallel()
        .Execute<ProcessImageStep>()
        .Execute<ProcessTextStep>()
        .Execute<ProcessMetadataStep>()
    .EndParallel()
    .Then<CombineResultsStep>();
```

## ⚙️ Configuration

### Dependency Injection Setup

```csharp
services.AddWorkflowEngine()
    .ConfigureWorkflowOptions(options =>
    {
        options.MaxRetryAttempts = 3;
        options.EnableExecutionTracing = true;
        options.EnableMemoryMetrics = true;
    });

// Auto-register workflows from assembly
services.AddWorkflowStepsFromAssembly(typeof(MyWorkflow).Assembly);

// Persistent workflows
services.AddPersistentWorkflowEngine();
services.AddWorkflowStateRepository<InMemoryWorkflowStateRepository>();
services.AddWorkflowNotificationService<EmailNotificationService>();
```

### Execution Options

```csharp
var options = new WorkflowExecutionOptions
{
    MaxRetryAttempts = 5,
    RetryBaseDelay = TimeSpan.FromMilliseconds(100),
    MaxRetryDelay = TimeSpan.FromMinutes(1),
    EnableExecutionTracing = true,
    EnableMemoryMetrics = true
};

var result = await engine.ExecuteAsync(workflow, context, options);
```

## 📊 Monitoring & Observability

### Execution Metrics

```csharp
var result = await engine.ExecuteAsync(workflow, context);

// Performance analysis
var executionTime = result.GetExecutionTime();
var performanceSummary = result.GetPerformanceSummary();
var longestStep = result.GetLongestRunningStep();
var failedSteps = result.GetFailedSteps();

Console.WriteLine($"Workflow completed in {executionTime}");
Console.WriteLine($"Steps executed: {result.Metrics?.StepsExecuted}");
Console.WriteLine($"Steps failed: {result.Metrics?.StepsFailed}");
```

### Execution Tracing

```csharp
foreach (var trace in result.ExecutionTrace ?? Enumerable.Empty<StepExecutionTrace>())
{
    Console.WriteLine($"{trace.StepName}: {trace.Result.IsSuccess}");
    if (!trace.Result.IsSuccess)
    {
        Console.WriteLine($"  Error: {trace.Result.ErrorMessage}");
        Console.WriteLine($"  Retries: {trace.RetryAttempts}");
    }
}
```

## 🛡️ Error Handling & Resilience

### Retry Policies

```csharp
public class ResilientStep : WorkflowStepBase<MyContext>
{
    public override bool CanRetry => true;
    public override TimeSpan? Timeout => TimeSpan.FromMinutes(5);

    public override async ValueTask<StepResult> ExecuteAsync(
        MyContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await SomeRiskyOperation();
            return Success();
        }
        catch (TransientException ex)
        {
            return Failure(ex.Message, shouldRetry: true);
        }
        catch (BusinessException ex)
        {
            return Failure(ex.Message, shouldRetry: false);
        }
    }
}
```

### Compensation Patterns

```csharp
public class CreateResourceStep : WorkflowStepBase<MyContext>
{
    public override async ValueTask<StepResult> ExecuteAsync(MyContext context, CancellationToken cancellationToken = default)
    {
        var resourceId = await _service.CreateResourceAsync();
        context.CreatedResourceId = resourceId;
        return Success();
    }

    public override async ValueTask<StepResult> CompensateAsync(MyContext context, CancellationToken cancellationToken = default)
    {
        if (context.CreatedResourceId != null)
        {
            await _service.DeleteResourceAsync(context.CreatedResourceId);
        }
        return Success();
    }
}
```

## 🧪 Testing

The library includes comprehensive test coverage with 100% pass rate:

```csharp
[Test]
public async Task Should_Execute_Sequential_Workflow()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddWorkflowEngine();
    services.AddWorkflow<TestWorkflow, TestContext>();
    
    var serviceProvider = services.BuildServiceProvider();
    var engine = serviceProvider.GetRequiredService<IWorkflowEngine>();
    var workflow = serviceProvider.GetRequiredService<TestWorkflow>();
    
    // Act
    var result = await engine.ExecuteAsync(workflow, new TestContext());
    
    // Assert
    Assert.That(result.IsSuccess, Is.True);
    Assert.That(result.Metrics?.StepsExecuted, Is.EqualTo(3));
}
```

## 📚 Documentation

For complete documentation, examples, and API reference:

- 📖 [Full Documentation](./DropBear.Codex.Workflow-Documentation.md)
- 🚀 [Quick Start Guide](#quick-start)
- 💡 [Examples & Patterns](#examples)
- 🔧 [Troubleshooting](#troubleshooting)

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

### Development Setup

```bash
git clone https://github.com/dropbear-software/workflow.git
cd workflow
dotnet restore
dotnet build
dotnet test
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with ❤️ by the DropBear Software team
- Inspired by modern workflow engines and best practices
- Special thanks to all contributors and testers

## 📞 Support

- 📧 Email: support@dropbear.dev
- 🐛 Issues: [GitHub Issues](https://github.com/dropbear-software/workflow/issues)
- 💬 Discussions: [GitHub Discussions](https://github.com/dropbear-software/workflow/discussions)
- 📖 Documentation: [docs.dropbear.dev](https://docs.dropbear.dev/workflow)

---

**Made with ❤️ by DropBear Software**