#region

using System.Diagnostics;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     A simple scope that measures elapsed time for an activity using <see cref="Stopwatch" />.
///     Disposable to ensure it stops measuring at the end of the scope.
/// </summary>
public sealed class ActivityScope : IDisposable
{
    private readonly string _name;
    private readonly Stopwatch _stopwatch;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new <see cref="ActivityScope" /> and starts measuring time immediately.
    /// </summary>
    /// <param name="name">A descriptive name for the scope (optional for logging/diagnostics).</param>
    public ActivityScope(string name)
    {
        _name = name;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    ///     Elapsed time measured so far.
    /// </summary>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    ///     Stops measuring time if not already stopped, then marks the scope as disposed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    /// <summary>
    ///     Explicitly stops measuring time. If called more than once, subsequent calls have no effect.
    /// </summary>
    public void Stop()
    {
        if (!_disposed)
        {
            _stopwatch.Stop();
        }
    }
}
