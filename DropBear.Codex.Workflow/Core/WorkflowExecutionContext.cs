using DropBear.Codex.Workflow.Configuration;
using DropBear.Codex.Workflow.Metrics;
using Serilog;

namespace DropBear.Codex.Workflow.Core;

/// <summary>
/// Internal execution context for workflow processing.
/// </summary>
internal sealed record WorkflowExecutionContext<TContext> where TContext : class
{
    public required WorkflowExecutionOptions Options { get; init; }
    public required string CorrelationId { get; init; }
    public required List<StepExecutionTrace> ExecutionTrace { get; init; }
    public required IServiceProvider ServiceProvider { get; init; }
    public required ILogger Logger { get; init; }
}
