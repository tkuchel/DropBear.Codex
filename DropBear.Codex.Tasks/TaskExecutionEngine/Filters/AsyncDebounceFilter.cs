#region

using MessagePipe;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

/// <summary>
///     Provides a filter for debouncing message handling, ensuring messages are handled only after a specified time
///     interval.
/// </summary>
/// <typeparam name="TMessage">The type of the message being filtered.</typeparam>
public sealed class AsyncDebounceFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly TimeSpan _debounceTime;
    private long _lastHandledTicks; // Stores the last handled time as ticks

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncDebounceFilter{TMessage}" /> class.
    /// </summary>
    /// <param name="debounceTime">The time interval for debouncing messages.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="debounceTime" /> is less than zero.</exception>
    public AsyncDebounceFilter(TimeSpan debounceTime)
    {
        if (debounceTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounceTime), "Debounce time cannot be negative.");
        }

        _debounceTime = debounceTime;
        _lastHandledTicks = DateTime.MinValue.Ticks;
    }

    /// <summary>
    ///     Handles the message if the debounce interval has elapsed since the last handled message.
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
        var nowTicks = DateTime.UtcNow.Ticks;

        // Thread-safe update of the last handled time
        var lastHandledTicks = Interlocked.Read(ref _lastHandledTicks);
        if (nowTicks - lastHandledTicks >= _debounceTime.Ticks)
        {
            Interlocked.Exchange(ref _lastHandledTicks, nowTicks);

            if (!cancellationToken.IsCancellationRequested)
            {
                await next(message, cancellationToken).ConfigureAwait(false);
            }
        }
        // Optionally log ignored messages for diagnostics
        // e.g., _logger.Debug("Message ignored due to debounce: {Message}", message);
    }
}
