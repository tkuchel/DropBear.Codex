namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public sealed record RetryConfiguration
{
    public int MaxRetryCount { get; init; }
    public TimeSpan RetryDelay { get; init; }
    public bool ContinueOnFailure { get; init; }
    public Func<Exception, bool>? RetryPredicate { get; init; }
}
