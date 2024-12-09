#region

using System.Collections.Concurrent;
using MessagePipe;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

/// <summary>
///     Provides a filter to suppress duplicate messages within a specified memory duration.
/// </summary>
/// <typeparam name="TMessage">The type of the message being filtered.</typeparam>
public class AsyncDuplicateMessageFilter<TMessage> : AsyncMessageHandlerFilter<TMessage> where TMessage : notnull
{
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _memoryDuration;
    private readonly ConcurrentDictionary<TMessage, DateTime> _recentMessages = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncDuplicateMessageFilter{TMessage}" /> class.
    /// </summary>
    /// <param name="memoryDuration">The duration for which duplicate messages are remembered.</param>
    public AsyncDuplicateMessageFilter(TimeSpan memoryDuration)
    {
        if (memoryDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(memoryDuration), "Memory duration must be greater than zero.");
        }

        _memoryDuration = memoryDuration;
        _cleanupTimer = new Timer(Cleanup, null, memoryDuration, memoryDuration);
    }

    /// <summary>
    ///     Handles the message if it is not a duplicate.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="next">The next handler in the pipeline.</param>
    /// <returns>A <see cref="ValueTask" /> representing the asynchronous operation.</returns>
    public override async ValueTask HandleAsync(
        TMessage message,
        CancellationToken cancellationToken,
        Func<TMessage, CancellationToken, ValueTask> next)
    {
        var now = DateTime.UtcNow;

        // Add message if not already present
        if (_recentMessages.TryAdd(message, now))
        {
            await next(message, cancellationToken).ConfigureAwait(false);
        }
        // Optionally log ignored duplicate
        // e.g., _logger.Debug("Duplicate message ignored: {Message}", message);
    }

    /// <summary>
    ///     Performs cleanup by removing expired entries from the message dictionary.
    /// </summary>
    /// <param name="state">The state object (unused).</param>
    private void Cleanup(object? state)
    {
        var expirationThreshold = DateTime.UtcNow - _memoryDuration;

        foreach (var (message, timestamp) in _recentMessages.ToArray())
        {
            if (timestamp < expirationThreshold)
            {
                _recentMessages.TryRemove(message, out _);
            }
        }
    }

    /// <summary>
    ///     Disposes of the resources used by the filter.
    /// </summary>
    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}
