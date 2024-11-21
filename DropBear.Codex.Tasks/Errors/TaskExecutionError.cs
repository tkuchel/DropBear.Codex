#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Tasks.Errors;

/// <summary>
///     Represents specific errors that can occur during task execution
/// </summary>
public record TaskExecutionError : ResultError
{
    public TaskExecutionError(string message, string? taskName = null, Exception? exception = null)
        : base(message)
    {
        TaskName = taskName;
        Exception = exception;
    }

    public string? TaskName { get; }
    public Exception? Exception { get; }
}
