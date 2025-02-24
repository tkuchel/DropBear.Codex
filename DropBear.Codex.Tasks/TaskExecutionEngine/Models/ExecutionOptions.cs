namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public sealed class ExecutionOptions
{
    public TimeSpan BatchingInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public int? MaxDegreeOfParallelism { get; set; }
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public bool EnableParallelExecution { get; set; } = false;
    public int ParallelBatchSize { get; set; } = 32;
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
