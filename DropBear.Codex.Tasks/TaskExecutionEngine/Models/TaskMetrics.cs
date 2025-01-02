namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Tracks metrics for task execution
/// </summary>
internal sealed class TaskMetrics
{
    private readonly object _lock = new();
    private int _executionCount;
    private int _failureCount;
    private long _totalExecutionTicks;

    public void RecordExecution(TimeSpan duration, bool succeeded)
    {
        lock (_lock)
        {
            _totalExecutionTicks += duration.Ticks;
            _executionCount++;
            if (!succeeded)
            {
                _failureCount++;
            }
        }
    }

    public (int ExecutionCount, int FailureCount, TimeSpan AverageExecutionTime) GetStats()
    {
        lock (_lock)
        {
            var averageTime = _executionCount > 0
                ? TimeSpan.FromTicks(_totalExecutionTicks / _executionCount)
                : TimeSpan.Zero;
            return (_executionCount, _failureCount, averageTime);
        }
    }
}
