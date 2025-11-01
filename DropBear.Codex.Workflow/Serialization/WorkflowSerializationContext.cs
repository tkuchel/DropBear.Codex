#region

using System.Text.Json.Serialization;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;
using DropBear.Codex.Workflow.Errors;
using DropBear.Codex.Workflow.Metrics;
using DropBear.Codex.Workflow.Results;

#endregion

namespace DropBear.Codex.Workflow.Serialization;

/// <summary>
///     JSON serialization context for DropBear.Codex.Workflow types.
///     Provides source-generated serialization for 20-30% performance improvement over reflection-based JSON serialization.
/// </summary>
/// <remarks>
///     This context includes all commonly serialized Workflow types:
///     - WorkflowResult{TContext} for various context types
///     - StepResult for step execution results
///     - WorkflowMetrics for execution metrics
///     - Workflow error types (WorkflowExecutionError, WorkflowConfigurationError, WorkflowStepTimeoutError)
///     - StepExecutionTrace for execution tracing
///     - CompensationFailure for Saga pattern compensation failures
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default,
    UseStringEnumConverter = true)]
// StepResult
[JsonSerializable(typeof(StepResult))]
// WorkflowMetrics
[JsonSerializable(typeof(WorkflowMetrics))]
// Workflow error types
[JsonSerializable(typeof(WorkflowExecutionError))]
[JsonSerializable(typeof(WorkflowConfigurationError))]
[JsonSerializable(typeof(WorkflowStepTimeoutError))]
// StepExecutionTrace (for execution tracing)
[JsonSerializable(typeof(StepExecutionTrace))]
[JsonSerializable(typeof(List<StepExecutionTrace>))]
// CompensationFailure (for Saga pattern)
[JsonSerializable(typeof(CompensationFailure))]
[JsonSerializable(typeof(List<CompensationFailure>))]
// Common Result types used in workflows
[JsonSerializable(typeof(Result<Unit, ResultError>))]
[JsonSerializable(typeof(Result<Unit, SimpleError>))]
[JsonSerializable(typeof(Result<Unit, OperationError>))]
// Common collections
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class WorkflowSerializationContext : JsonSerializerContext
{
}
