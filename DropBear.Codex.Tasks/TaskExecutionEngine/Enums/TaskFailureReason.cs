namespace DropBear.Codex.Tasks.TaskExecutionEngine.Enums;

/// <summary>
///     Reason for task failure
/// </summary>
public enum TaskFailureReason
{
    ValidationFailed,
    DependencyFailed,
    ExecutionFailed,
    CompensationFailed,
    Cancelled,
    Unknown
}
