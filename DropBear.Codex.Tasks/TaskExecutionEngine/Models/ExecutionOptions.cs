#region

using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Configurable options for an execution engine, including batching intervals,
///     parallelism, and retry behaviors.
/// </summary>
public sealed class ExecutionOptions
{
    /// <summary>
    ///     Interval at which certain events (e.g., progress updates) may be batched or polled.
    /// </summary>
    public TimeSpan BatchingInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///     Maximum degree of parallelism for task execution, or <c>null</c> to use <c>Environment.ProcessorCount</c>.
    /// </summary>
    public int? MaxDegreeOfParallelism { get; set; }

    /// <summary>
    ///     Default timeout for tasks that don't specify their own.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Maximum number of retry attempts for tasks that fail.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    ///     Delay between retry attempts for tasks that fail.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     If <c>true</c>, tasks are executed in parallel (subject to <see cref="MaxDegreeOfParallelism" />).
    ///     This is kept for backward compatibility; new code should use <see cref="ExecutionStrategy" /> instead.
    /// </summary>
    public bool EnableParallelExecution { get; set; } = false;

    /// <summary>
    ///     The batch size for parallel scheduling (if <see cref="EnableParallelExecution" /> is <c>true</c>).
    /// </summary>
    public int ParallelBatchSize { get; set; } = 32;

    /// <summary>
    ///     If <c>true</c>, stops execution on the first task failure (incompatible with <see cref="EnableParallelExecution" />
    ///     ).
    /// </summary>
    public bool StopOnFirstFailure { get; set; } = false;

    /// <summary>
    ///     If <c>true</c>, provides more detailed logs during execution.
    /// </summary>
    public bool VerboseLogging { get; set; } = false;

    /// <summary>
    ///     The strategy to use when executing tasks.
    ///     If null, defaults to Adaptive which selects the best strategy based on task characteristics.
    /// </summary>
    public ExecutionStrategy? ExecutionStrategy { get; set; } = null;

    /// <summary>
    ///     The minimum number of available processor cores required to consider parallel execution.
    ///     On machines with fewer cores, sequential execution will be chosen automatically.
    /// </summary>
    public int MinimumCoresForParallelExecution { get; set; } = 2;

    /// <summary>
    ///     The threshold of dependencies beyond which tasks will be executed sequentially.
    ///     A value between 0.0 and 1.0 representing the ratio of actual dependencies to maximum possible dependencies.
    ///     Default is 0.5 (50% dependency density).
    /// </summary>
    public double DependencyDensityThreshold { get; set; } = 0.5;

    /// <summary>
    ///     Validates the consistency of these options.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if <see cref="EnableParallelExecution" /> is incompatible with <see cref="StopOnFirstFailure" />.
    /// </exception>
    public void Validate()
    {
        if (EnableParallelExecution && StopOnFirstFailure)
        {
            throw new InvalidOperationException("EnableParallelExecution cannot be used with StopOnFirstFailure.");
        }

        if (MinimumCoresForParallelExecution < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumCoresForParallelExecution),
                "Minimum cores for parallel execution must be at least 1.");
        }

        if (DependencyDensityThreshold < 0.0 || DependencyDensityThreshold > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(DependencyDensityThreshold),
                "Dependency density threshold must be between 0.0 and 1.0.");
        }
    }
}
