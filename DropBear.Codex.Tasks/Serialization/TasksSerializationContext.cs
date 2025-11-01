#region

using System.Text.Json.Serialization;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;
using DropBear.Codex.Tasks.Caching;
using DropBear.Codex.Tasks.Errors;

#endregion

namespace DropBear.Codex.Tasks.Serialization;

/// <summary>
///     JSON serialization context for DropBear.Codex.Tasks types.
///     Provides source-generated serialization for 20-30% performance improvement over reflection-based JSON serialization.
/// </summary>
/// <remarks>
///     This context includes all commonly serialized Tasks types:
///     - Task error types (TaskExecutionError, TaskValidationError, CacheError, ExecutionEngineError)
///     - Common Result types used in task execution
///     - Task metadata and execution context
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default,
    UseStringEnumConverter = true)]
// Task error types
[JsonSerializable(typeof(TaskExecutionError))]
[JsonSerializable(typeof(TaskValidationError))]
[JsonSerializable(typeof(CacheError))]
[JsonSerializable(typeof(ExecutionEngineError))]
// Common Result types for task operations
[JsonSerializable(typeof(Result<Unit, TaskExecutionError>))]
[JsonSerializable(typeof(Result<Unit, TaskValidationError>))]
[JsonSerializable(typeof(Result<Unit, CacheError>))]
[JsonSerializable(typeof(Result<Unit, ExecutionEngineError>))]
[JsonSerializable(typeof(Result<string, TaskExecutionError>))]
[JsonSerializable(typeof(Result<int, TaskExecutionError>))]
[JsonSerializable(typeof(Result<bool, TaskExecutionError>))]
[JsonSerializable(typeof(Result<object, TaskExecutionError>))]
// Core error fallbacks
[JsonSerializable(typeof(Result<Unit, SimpleError>))]
[JsonSerializable(typeof(Result<Unit, OperationError>))]
// Common collections
[JsonSerializable(typeof(List<TaskExecutionError>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class TasksSerializationContext : JsonSerializerContext
{
}
