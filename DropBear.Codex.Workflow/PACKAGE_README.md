# DropBear.Codex.Workflow

[![NuGet](https://img.shields.io/nuget/v/DropBear.Codex.Workflow.svg)](https://www.nuget.org/packages/DropBear.Codex.Workflow/)
[![Downloads](https://img.shields.io/nuget/dt/DropBear.Codex.Workflow.svg)](https://www.nuget.org/packages/DropBear.Codex.Workflow/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Workflow engine for .NET 9+ applications providing fluent workflow building, step execution, retry logic, compensation, and persistent workflow management.

## 🚀 Quick Start

### Installation

```bash
dotnet add package DropBear.Codex.Workflow
```

### Basic Usage

```csharp
using DropBear.Codex.Workflow;
using DropBear.Codex.Workflow.Builder;
using DropBear.Codex.Workflow.Core;

// Define your workflow context
public sealed class OrderContext
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public bool IsApproved { get; set; }
}

// Define workflow steps
public sealed class ValidateOrderStep : WorkflowStepBase<OrderContext>
{
    public override string StepName => "ValidateOrder";
    
    public override async ValueTask<StepResult> ExecuteAsync(
        OrderContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Amount <= 0)
            return Failure("Order amount must be greater than zero");
            
        return Success();
    }
}

public sealed class ProcessPaymentStep : WorkflowStepBase<OrderContext>
{
    public override string StepName => "ProcessPayment";
    public override bool CanRetry => true;  // Enable automatic retry on failure
    
    public override async ValueTask<StepResult> ExecuteAsync(
        OrderContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simulate payment processing
            await _paymentService.ProcessAsync(context.Amount, cancellationToken);
            context.IsApproved = true;
            return Success();
        }
        catch (TransientException ex)
        {
            // Will be automatically retried with exponential backoff
            return Failure(ex, shouldRetry: true);
        }
    }
}

// Build and execute workflow
var workflow = new WorkflowBuilder<OrderContext>("order-workflow", "Order Processing")
    .StartWith<ValidateOrderStep>()
    .Then<ProcessPaymentStep>()
    .Build();

var context = new OrderContext { OrderId = 123, Amount = 99.99m };
var engine = serviceProvider.GetRequiredService<IWorkflowEngine>();

var result = await engine.ExecuteAsync(workflow, context);

if (result.IsSuccess)
{
    Console.WriteLine($"✓ Order processed successfully");
    Console.WriteLine($"  Approved: {context.IsApproved}");
    Console.WriteLine($"  Execution time: {result.GetExecutionTime()}");
}
else
{
    Console.WriteLine($"✗ Workflow failed: {result.ErrorMessage}");
}
```

## ✨ Features

### Fluent Workflow Building
Build complex workflows with a clean, intuitive API:

```csharp
var workflow = new WorkflowBuilder<MyContext>("approval-workflow", "Approval Workflow")
    .WithTimeout(TimeSpan.FromMinutes(5))
    .StartWith<ValidateRequestStep>()
    .Then<CheckInventoryStep>()
    .If(ctx => ctx.Amount > 1000)
        .ThenExecute<ManagerApprovalStep>()
        .ElseExecute<AutoApproveStep>()
        .EndIf()
    .InParallel()
        .Execute<SendEmailStep>()
        .Execute<LogEventStep>()
        .Execute<UpdateDashboardStep>()
        .EndParallel()
    .Then<FinalizeOrderStep>()
    .Build();
```

### Automatic Retry with Exponential Backoff
```csharp
public sealed class ApiCallStep : WorkflowStepBase<MyContext>
{
    public override bool CanRetry => true;
    public override TimeSpan? Timeout => TimeSpan.FromSeconds(30);
    
    public override async ValueTask<StepResult> ExecuteAsync(
        MyContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.PostAsync(/* ... */, cancellationToken);
            response.EnsureSuccessStatusCode();
            return Success();
        }
        catch (HttpRequestException ex) when (IsTransient(ex))
        {
            // Automatically retried with exponential backoff
            return Failure(ex, shouldRetry: true);
        }
    }
}
```

### Persistent Workflows with Signal Support
```csharp
// Start a long-running workflow that waits for external signals
var result = await persistentEngine.StartPersistentWorkflowAsync(
    workflow,
    context);

Console.WriteLine($"Workflow started: {result.WorkflowInstanceId}");

// Later, when approval is received, signal the workflow to continue
await persistentEngine.SignalWorkflowAsync(
    result.WorkflowInstanceId,
    "ApprovalReceived",
    new ApprovalData { Approved = true, ApproverName = "John Doe" });
```

### Compensation (Saga Pattern)
```csharp
public sealed class CreateOrderStep : WorkflowStepBase<OrderContext>
{
    public override async ValueTask<StepResult> ExecuteAsync(
        OrderContext context,
        CancellationToken cancellationToken = default)
    {
        context.OrderId = await _orderService.CreateAsync(context);
        return Success();
    }
    
    // If workflow fails, this compensation runs to rollback
    public override async ValueTask<StepResult> CompensateAsync(
        OrderContext context,
        CancellationToken cancellationToken = default)
    {
        await _orderService.CancelAsync(context.OrderId);
        return Success();
    }
}
```

### Execution Metrics and Tracing
```csharp
var result = await engine.ExecuteAsync(workflow, context);

Console.WriteLine($"Execution Summary:");
Console.WriteLine($"  Total time: {result.GetExecutionTime()}");
Console.WriteLine($"  Steps executed: {result.Metrics?.StepsExecuted}");
Console.WriteLine($"  Failed steps: {result.Metrics?.StepsFailed}");
Console.WriteLine($"  Retry attempts: {result.Metrics?.RetryAttempts}");

// Get detailed step-by-step trace
if (result.HasExecutionTrace())
{
    Console.WriteLine($"
Execution Trace:");
    foreach (var trace in result.ExecutionTrace ?? [])
    {
        var duration = trace.EndTime - trace.StartTime;
        var status = trace.Result.IsSuccess ? "✓" : "✗";
        Console.WriteLine($"  {status} {trace.StepName}: {duration.TotalMilliseconds:F2}ms");
    }
}

// Get performance summary
var summary = result.GetPerformanceSummary();
Console.WriteLine($"
Performance:");
Console.WriteLine($"  Average step time: {summary["AverageStepTime"]}");
Console.WriteLine($"  Longest step: {summary["LongestStepTime"]}");
Console.WriteLine($"  Peak memory: {summary["PeakMemoryUsageMB"]}MB");
```

### Conditional Branching
```csharp
var workflow = new WorkflowBuilder<OrderContext>("order-workflow", "Order Processing")
    .StartWith<ValidateOrderStep>()
    .If(ctx => ctx.IsVipCustomer)
        .ThenExecute<FastTrackStep>()
        .ElseExecute<StandardProcessingStep>()
        .EndIf()
    .Then<FinalizeStep>()
    .Build();
```

### Parallel Execution
```csharp
var workflow = new WorkflowBuilder<OrderContext>("order-workflow", "Order Processing")
    .StartWith<PrepareOrderStep>()
    .InParallel()
        .Execute<ChargePaymentStep>()
        .Execute<ReserveInventoryStep>()
        .Execute<SendConfirmationStep>()
        .EndParallel()
    .Then<ShipOrderStep>()
    .Build();
```

### Delays and Scheduling
```csharp
var workflow = new WorkflowBuilder<OrderContext>("reminder-workflow", "Reminder Workflow")
    .StartWith<SendInitialEmailStep>()
    .Delay(TimeSpan.FromHours(24))  // Wait 24 hours
    .Then<SendReminderEmailStep>()
    .Delay(TimeSpan.FromDays(3))    // Wait 3 days
    .Then<SendFinalReminderStep>()
    .Build();
```

## 📦 Dependency Injection Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using DropBear.Codex.Workflow.Extensions;

// Register workflow engine
services.AddWorkflowEngine();

// Register individual steps
services.AddScoped<ValidateOrderStep>();
services.AddScoped<ProcessPaymentStep>();
services.AddScoped<SendEmailStep>();

// Register workflow definition
services.AddWorkflow<OrderWorkflowDefinition, OrderContext>();

// For persistent workflows
services.AddPersistentWorkflow<OrderContext>(options =>
{
    options.EnableTimeoutProcessing = true;
    options.TimeoutCheckInterval = TimeSpan.FromMinutes(5);
});

// Register state repository (implement IWorkflowStateRepository)
services.AddSingleton<IWorkflowStateRepository, YourStateRepository>();
```

## 🎯 Key Benefits

- ✅ **Type-Safe** - Compile-time workflow validation
- ✅ **Fluent API** - Intuitive, readable workflow building
- ✅ **Persistent** - Long-running workflows with signal support
- ✅ **Resilient** - Automatic retry with exponential backoff
- ✅ **Compensating** - Built-in saga pattern support for rollbacks
- ✅ **Observable** - Detailed metrics and execution tracing
- ✅ **Flexible** - Conditional branches, parallel execution, delays
- ✅ **Testable** - Easy to unit test individual steps and workflows
- ✅ **Production-Ready** - Battle-tested patterns and best practices

## 📖 Documentation

- [Full Documentation](https://github.com/tkuchel/DropBear.Codex/wiki/Workflow)
- [API Reference](https://github.com/tkuchel/DropBear.Codex/wiki/Workflow-API)
- [Examples & Recipes](https://github.com/tkuchel/DropBear.Codex/wiki/Workflow-Examples)
- [Migration Guide](https://github.com/tkuchel/DropBear.Codex/wiki/Workflow-Migration)

## 🔧 Requirements

- .NET 9.0 or later
- C# 12 or later
- DropBear.Codex.Core 2025.10.0 or later

## 📦 Related Packages

- **DropBear.Codex.Core** - Core functionality and Result pattern
- **DropBear.Codex.StateManagement** - Advanced state management
- **DropBear.Codex.Serialization** - Advanced serialization support

## 📝 Example Scenarios

### E-Commerce Order Processing
```csharp
// Handle complete order lifecycle with compensation
var orderWorkflow = new WorkflowBuilder<OrderContext>("order", "Order Processing")
    .StartWith<ValidateOrderStep>()
    .Then<ReserveInventoryStep>()    // Compensatable
    .Then<ChargePaymentStep>()       // Compensatable
    .Then<CreateShipmentStep>()      // Compensatable
    .Then<SendConfirmationStep>()
    .Build();
```

### Approval Workflows
```csharp
// Multi-level approval with timeout handling
var approvalWorkflow = new WorkflowBuilder<ApprovalContext>("approval", "Approval")
    .StartWith<CreateRequestStep>()
    .If(ctx => ctx.Amount < 1000)
        .ThenExecute<AutoApproveStep>()
        .ElseExecute<ManagerApprovalStep>()  // Waits for signal
        .EndIf()
    .If(ctx => ctx.Amount > 10000)
        .ThenExecute<DirectorApprovalStep>()  // Waits for signal
        .EndIf()
    .Then<ExecuteApprovedActionStep>()
    .Build();
```

### Data Processing Pipelines
```csharp
// Parallel data processing with error handling
var pipelineWorkflow = new WorkflowBuilder<DataContext>("pipeline", "Data Pipeline")
    .StartWith<ExtractDataStep>()
    .InParallel()
        .Execute<TransformCustomersStep>()
        .Execute<TransformOrdersStep>()
        .Execute<TransformProductsStep>()
        .EndParallel()
    .Then<LoadDataStep>()
    .Then<ValidateDataStep>()
    .Build();
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/tkuchel/DropBear.Codex/blob/master/LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please read our [Contributing Guidelines](https://github.com/tkuchel/DropBear.Codex/blob/master/CONTRIBUTING.md) first.

## 💬 Support

- 📧 Email: dropbear.noreply@pm.me
- 🐛 Issues: [GitHub Issues](https://github.com/tkuchel/DropBear.Codex/issues)
- 💡 Discussions: [GitHub Discussions](https://github.com/tkuchel/DropBear.Codex/discussions)

---

Made with ❤️ by [Terrence Kuchel](https://github.com/tkuchel)
