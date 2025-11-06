using System.Runtime.CompilerServices;

namespace DropBear.Codex.Blazor.Extensions;

/// <summary>
/// Extension methods for SemaphoreSlim to provide async disposable lock pattern
/// Leverages .NET 9+ features for improved performance
/// </summary>
public static class SemaphoreSlimExtensions
{
    /// <summary>
    /// Acquires a SemaphoreSlim lock that can be used with 'await using' pattern
    /// </summary>
    /// <param name="semaphore">The semaphore to lock</param>
    /// <param name="timeout">Maximum time to wait for the lock</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An async disposable lock</returns>
    public static async ValueTask<SemaphoreLock> LockAsync(
        this SemaphoreSlim semaphore,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var acquired = await semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        return new SemaphoreLock(semaphore, acquired);
    }

    /// <summary>
    /// Acquires a SemaphoreSlim lock that can be used with 'await using' pattern
    /// </summary>
    /// <param name="semaphore">The semaphore to lock</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An async disposable lock</returns>
    public static async ValueTask<SemaphoreLock> LockAsync(
        this SemaphoreSlim semaphore,
        CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new SemaphoreLock(semaphore, true);
    }
}

/// <summary>
/// Represents a lock on a SemaphoreSlim that automatically releases when disposed
/// </summary>
public readonly struct SemaphoreLock : IAsyncDisposable
{
    private readonly SemaphoreSlim? _semaphore;
    private readonly bool _acquired;

    internal SemaphoreLock(SemaphoreSlim semaphore, bool acquired)
    {
        _semaphore = semaphore;
        _acquired = acquired;
    }

    /// <summary>
    /// Gets whether the lock was successfully acquired
    /// </summary>
    public bool IsAcquired => _acquired;

    /// <summary>
    /// Releases the semaphore if it was acquired
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync()
    {
        if (_acquired && _semaphore != null)
        {
            _semaphore.Release();
        }

        return ValueTask.CompletedTask;
    }
}
