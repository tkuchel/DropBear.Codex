#region

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Tracks a smooth progress transition, including the current progress and
///     cancellation mechanism for an in-progress animation.
/// </summary>
internal sealed class ProgressTransition : IDisposable
{
    private readonly TaskCompletionSource<bool> _completionSource = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _isDisposed;

    /// <summary>
    ///     Gets the current progress value (e.g., 0-100 for percentage).
    /// </summary>
    public double CurrentProgress { get; private set; }

    /// <summary>
    ///     Indicates whether the transition has finished (either success or cancellation).
    /// </summary>
    public bool IsComplete => _completionSource.Task.IsCompleted;

    /// <summary>
    ///     A token used to signal cancellation of this transition's animation/logic.
    /// </summary>
    public CancellationToken CancellationToken => _cts.Token;

    /// <summary>
    ///     Disposes of resources, signaling cancellation if necessary.
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
    ///     Updates the current progress value of this transition.
    /// </summary>
    /// <param name="progress">New progress value.</param>
    public void UpdateProgress(double progress)
    {
        CurrentProgress = progress;
    }

    /// <summary>
    ///     Completes the transition, marking it as succeeded or canceled.
    /// </summary>
    /// <param name="success">True if the transition completed successfully; false otherwise.</param>
    public void Complete(bool success = true)
    {
        // Once set, the TaskCompletionSource cannot be reset
        _completionSource.TrySetResult(success);
    }
}
