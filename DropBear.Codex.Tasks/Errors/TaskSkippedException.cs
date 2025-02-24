namespace DropBear.Codex.Tasks.Errors;

/// <summary>
///     Exception indicating that a task was intentionally skipped.
/// </summary>
public class TaskSkippedException : Exception
{
    /// <summary>
    ///     Initializes a new instance of <see cref="TaskSkippedException" /> with a task name.
    /// </summary>
    /// <param name="taskName">The name of the task that was skipped.</param>
    public TaskSkippedException(string taskName)
        : base($"Task '{taskName}' was skipped.")
    {
        TaskName = taskName;
    }

    /// <summary>
    ///     Gets the name of the task that was skipped.
    /// </summary>
    public string TaskName { get; }
}
