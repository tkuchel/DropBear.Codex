namespace DropBear.Codex.Tasks.Errors;

public class TaskSkippedException : Exception
{
    public TaskSkippedException(string taskName)
        : base($"Task '{taskName}' was skipped.")
    {
        TaskName = taskName;
    }

    public string TaskName { get; }
}
