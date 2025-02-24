namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public class TaskExecutionMetrics
{
    private readonly object _lock = new();
    private int _failureCount;
    private int _successCount;
    private long _totalDurationTicks;

    public void RecordSuccess(TimeSpan duration)
    {
        lock (_lock)
        {
            _successCount++;
            _totalDurationTicks += duration.Ticks;
        }
    }

    public void RecordFailure(TimeSpan duration)
    {
        lock (_lock)
        {
            _failureCount++;
            _totalDurationTicks += duration.Ticks;
        }
    }

    public (int SuccessCount, int FailureCount, TimeSpan AverageDuration) GetMetrics()
    {
        lock (_lock)
        {
            var totalCount = _successCount + _failureCount;
            var avgDuration = totalCount > 0
                ? TimeSpan.FromTicks(_totalDurationTicks / totalCount)
                : TimeSpan.Zero;
            return (_successCount, _failureCount, avgDuration);
        }
    }
}
