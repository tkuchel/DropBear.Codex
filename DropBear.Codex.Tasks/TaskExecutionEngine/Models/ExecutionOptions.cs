namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public sealed class ExecutionOptions
{
    public bool EnableParallelExecution { get; set; } = false;
    public bool StopOnFirstFailure { get; set; } = false;
    public bool VerboseLogging { get; set; } = false;

    public void Validate()
    {
        if (EnableParallelExecution && StopOnFirstFailure)
        {
            throw new InvalidOperationException("EnableParallelExecution cannot be used with StopOnFirstFailure.");
        }
    }
}
