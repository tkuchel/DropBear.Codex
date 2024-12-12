namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Helper class for tracking progress transitions
/// </summary>
internal sealed class ProgressTransition : IDisposable
{
    private readonly TaskCompletionSource<bool> _completionSource = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _isDisposed;

    /// <summary>
    ///     Gets the current progress value
    /// </summary>
    public double CurrentProgress { get; private set; }

    /// <summary>
    ///     Gets whether the transition is complete
    /// </summary>
    public bool IsComplete => _completionSource.Task.IsCompleted;

    /// <summary>
    ///     Gets the cancellation token for this transition
    /// </summary>
    public CancellationToken CancellationToken => _cts.Token;

    /// <summary>
    ///     Disposes resources
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            _cts.Cancel();
            _cts.Dispose();
            _completionSource.TrySetCanceled();
        }
        catch
        {
            // Ignore disposal errors
        }
    }

    /// <summary>
    ///     Updates the current progress value
    /// </summary>
    public void UpdateProgress(double progress)
    {
        CurrentProgress = progress;
    }

    /// <summary>
    ///     Completes the transition
    /// </summary>
    public void Complete(bool success = true)
    {
        _completionSource.TrySetResult(success);
    }
}
