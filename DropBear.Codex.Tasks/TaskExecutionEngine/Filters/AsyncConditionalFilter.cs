#region

using MessagePipe;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

/// <summary>
///     Provides a filter for conditionally processing messages based on a user-defined predicate.
/// </summary>
/// <typeparam name="TMessage">The type of the message being filtered.</typeparam>
public sealed class AsyncConditionalFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly Func<TMessage, bool> _predicate;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncConditionalFilter{TMessage}" /> class.
    /// </summary>
    /// <param name="predicate">A predicate function to determine if a message should be processed.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate" /> is null.</exception>
    public AsyncConditionalFilter(Func<TMessage, bool> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <summary>
    ///     Conditionally processes the message if it matches the predicate.
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
        if (cancellationToken.IsCancellationRequested)
        {
            return; // Exit early if canceled
        }

        if (_predicate(message))
        {
            await next(message, cancellationToken).ConfigureAwait(false);
        }
        // Optionally log ignored messages for debugging purposes
        // e.g., _logger.Debug("Message ignored by filter: {Message}", message);
    }
}
