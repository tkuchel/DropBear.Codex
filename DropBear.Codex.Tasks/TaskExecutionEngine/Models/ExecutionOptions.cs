﻿namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

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
    }
}
