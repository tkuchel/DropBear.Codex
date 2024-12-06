using System.Collections.Concurrent;

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public sealed class TaskExecutionStats
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int FailedTasks { get; set; }
    public int SkippedTasks { get; set; }
    public ConcurrentDictionary<string, TimeSpan> TaskDurations { get; init; } = new(StringComparer.Ordinal);
}
