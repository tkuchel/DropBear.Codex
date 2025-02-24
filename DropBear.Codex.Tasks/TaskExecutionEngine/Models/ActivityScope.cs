#region

using System.Diagnostics;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public sealed class ActivityScope : IDisposable
{
    private readonly string _name;
    private readonly Stopwatch _stopwatch;
    private bool _disposed;

    public ActivityScope(string name)
    {
        _name = name;
        _stopwatch = Stopwatch.StartNew();
    }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    public void Stop()
    {
        if (!_disposed)
        {
            _stopwatch.Stop();
        }
    }
}
