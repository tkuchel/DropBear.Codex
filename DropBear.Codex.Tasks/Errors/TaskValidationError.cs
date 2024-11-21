namespace DropBear.Codex.Tasks.Errors;

public sealed record TaskValidationError : TaskExecutionError
{
    public TaskValidationError(string message, string taskName, Exception? exception = null)
        : base($"Task Validation Error: {message}", taskName, exception)
    {
    }
}
