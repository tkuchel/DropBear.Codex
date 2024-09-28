#region

using System.Collections.Concurrent;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Represents the execution context shared among tasks during execution.
///     Provides access to shared data, logging, and execution options.
/// </summary>
public sealed class ExecutionContext
{
    private int _completedTaskCount;

    private int _totalTaskCount;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExecutionContext" /> class.
    /// </summary>
    /// <param name="logger">The logger instance to be used by tasks.</param>
    /// <param name="options">The execution options that configure task execution behavior.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger" /> or <paramref name="options" /> is null.</exception>
    public ExecutionContext(ILogger logger, ExecutionOptions options)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    ///     Gets the thread-safe dictionary for storing shared data between tasks.
    /// </summary>
    public ConcurrentDictionary<string, object> Data { get; } = new(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the logger instance for logging within tasks.
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    ///     Gets the execution options that configure task execution behavior.
    /// </summary>
    public ExecutionOptions Options { get; }

    /// <summary>
    ///     Gets or sets the total number of tasks to be executed.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    public int TotalTaskCount
    {
        get => _totalTaskCount;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(TotalTaskCount), "TotalTaskCount cannot be negative.");
            }

            _totalTaskCount = value;
        }
    }

    /// <summary>
    ///     Gets the number of tasks that have completed execution.
    /// </summary>
    public int CompletedTaskCount => _completedTaskCount;

    /// <summary>
    ///     Increments the <see cref="CompletedTaskCount" /> in a thread-safe manner.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the incremented value exceeds <see cref="TotalTaskCount" />.</exception>
    public void IncrementCompletedTaskCount()
    {
        var newValue = Interlocked.Increment(ref _completedTaskCount);
        if (newValue > _totalTaskCount)
        {
            throw new InvalidOperationException("CompletedTaskCount cannot exceed TotalTaskCount.");
        }
    }
}
