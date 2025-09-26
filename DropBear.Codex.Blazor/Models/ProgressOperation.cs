using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Components.Progress;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Extensions;
using Microsoft.Extensions.Logging;

namespace DropBear.Codex.Blazor.Models;

// <summary>
///     Represents a progress operation with automatic cleanup and state management.
/// </summary>
public sealed class ProgressOperation : IDisposable
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, ProgressStepState> _stepStates = [];
    private readonly object _lock = new();

    private double _overallProgress;
    private string _message = string.Empty;
    private DateTime _startTime = DateTime.UtcNow;
    private DateTime _lastUpdateTime = DateTime.UtcNow;
    private bool _isCompleted;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new progress operation.
    /// </summary>
    /// <param name="operationId">Unique identifier for this operation.</param>
    /// <param name="steps">Optional collection of steps.</param>
    /// <param name="logger">Logger instance.</param>
    public ProgressOperation(
        string operationId,
        IReadOnlyList<ProgressStepConfig>? steps,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(operationId);
        ArgumentNullException.ThrowIfNull(logger);

        OperationId = operationId;
        Steps = steps;
        _logger = logger;

        // Initialize step states
        if (steps != null)
        {
            foreach (var step in steps)
            {
                _stepStates[step.Id] = new ProgressStepState(step.Id);
            }
        }
    }

    /// <summary>
    ///     Gets the unique operation identifier.
    /// </summary>
    public string OperationId { get; }

    /// <summary>
    ///     Gets the configured steps for this operation.
    /// </summary>
    public IReadOnlyList<ProgressStepConfig>? Steps { get; }

    /// <summary>
    ///     Gets the overall progress (0-100).
    /// </summary>
    public double OverallProgress
    {
        get
        {
            lock (_lock)
            {
                return _overallProgress;
            }
        }
    }

    /// <summary>
    ///     Gets the current message.
    /// </summary>
    public string Message
    {
        get
        {
            lock (_lock)
            {
                return _message;
            }
        }
    }

    /// <summary>
    ///     Gets the elapsed time since operation started.
    /// </summary>
    public TimeSpan ElapsedTime => DateTime.UtcNow - _startTime;

    /// <summary>
    ///     Gets whether this operation has completed.
    /// </summary>
    public bool IsCompleted
    {
        get
        {
            lock (_lock)
            {
                return _isCompleted;
            }
        }
    }

    /// <summary>
    ///     Gets whether this operation has expired and should be cleaned up.
    /// </summary>
    public bool IsExpired => IsCompleted && DateTime.UtcNow - _lastUpdateTime > TimeSpan.FromMinutes(30);

    /// <summary>
    ///     Updates the overall progress.
    /// </summary>
    /// <param name="progress">Progress value (0-100).</param>
    /// <param name="message">Optional message.</param>
    public void UpdateProgress(double progress, string? message = null)
    {
        ObjectDisposedException.ThrowIfDisposed(_disposed, this);

        lock (_lock)
        {
            _overallProgress = Math.Clamp(progress, 0, 100);
            if (message != null) _message = message;
            _lastUpdateTime = DateTime.UtcNow;
            _isCompleted = _overallProgress >= 100;
        }
    }

    /// <summary>
    ///     Updates a specific step's progress.
    /// </summary>
    /// <param name="stepId">Step identifier.</param>
    /// <param name="progress">Progress value (0-100).</param>
    /// <param name="status">Step status.</param>
    public void UpdateStepProgress(string stepId, double progress, StepStatus status)
    {
        ObjectDisposedException.ThrowIfDisposed(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(stepId);

        lock (_lock)
        {
            if (_stepStates.TryGetValue(stepId, out var stepState))
            {
                stepState.UpdateProgress(progress, status);
                _lastUpdateTime = DateTime.UtcNow;

                // Calculate overall progress from step states
                if (_stepStates.Count > 0)
                {
                    _overallProgress = _stepStates.Values.Average(s => s.Progress);
                    _isCompleted = _stepStates.Values.All(s => s.IsCompleted);
                }
            }
        }
    }

    /// <summary>
    ///     Gets the current state of a step.
    /// </summary>
    /// <param name="stepId">Step identifier.</param>
    /// <returns>The step state, or null if not found.</returns>
    public ProgressStepState? GetStepState(string stepId)
    {
        ArgumentException.ThrowIfNullOrEmpty(stepId);

        lock (_lock)
        {
            return _stepStates.TryGetValue(stepId, out var state) ? state : null;
        }
    }

    /// <summary>
    ///     Gets all step states.
    /// </summary>
    /// <returns>A read-only collection of step states.</returns>
    public IReadOnlyCollection<ProgressStepState> GetAllStepStates()
    {
        lock (_lock)
        {
            return _stepStates.Values.ToArray();
        }
    }

    /// <summary>
    ///     Marks the operation as completed.
    /// </summary>
    public void Complete()
    {
        ObjectDisposedException.ThrowIfDisposed(_disposed, this);

        lock (_lock)
        {
            _overallProgress = 100;
            _isCompleted = true;
            _lastUpdateTime = DateTime.UtcNow;

            // Mark all incomplete steps as completed
            foreach (var stepState in _stepStates.Values)
            {
                if (!stepState.IsCompleted && !stepState.HasFailed)
                {
                    stepState.UpdateProgress(100, StepStatus.Completed);
                }
            }
        }
    }

    /// <summary>
    ///     Disposes the operation and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _stepStates.Clear();
            _disposed = true;
        }

        _logger.LogDebug("Progress operation {OperationId} disposed", OperationId);
    }
}
