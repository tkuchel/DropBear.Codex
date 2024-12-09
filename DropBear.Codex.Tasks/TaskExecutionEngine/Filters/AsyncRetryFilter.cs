#region

using DropBear.Codex.Core.Logging;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

/// <summary>
///     Provides retry logic for asynchronous message handling.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
public sealed class AsyncRetryFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly ILogger _logger;
    private readonly int _maxRetryCount;
    private readonly TimeSpan _retryDelay;
    private readonly Func<Exception, bool>? _shouldRetry;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncRetryFilter{TMessage}" /> class.
    /// </summary>
    /// <param name="maxRetryCount">The maximum number of retry attempts.</param>
    /// <param name="retryDelay">The delay between retry attempts.</param>
    /// <param name="shouldRetry">Optional predicate to determine if an exception is retryable.</param>
    public AsyncRetryFilter(
        int maxRetryCount,
        TimeSpan retryDelay,
        Func<Exception, bool>? shouldRetry = null)
    {
        if (maxRetryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), "Retry count must be non-negative.");
        }

        if (retryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay), "Retry delay must be non-negative.");
        }

        _maxRetryCount = maxRetryCount;
        _retryDelay = retryDelay;
        _shouldRetry = shouldRetry;
        _logger = LoggerFactory.Logger.ForContext(typeof(TMessage));
    }

    /// <summary>
    ///     Handles retries for message handling failures.
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
        var attempt = 0;

        while (true)
        {
            try
            {
                attempt++;
                await next(message, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt <= _maxRetryCount && ShouldRetry(ex))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Warning(
                        "Retry operation cancelled for message of type {MessageType} after {Attempt} attempts.",
                        typeof(TMessage).Name, attempt);
                    throw;
                }

                _logger.Warning(ex,
                    "Exception occurred while handling message of type {MessageType}. Attempt {Attempt} of {MaxRetryCount}. Retrying after {RetryDelay} ms.",
                    typeof(TMessage).Name, attempt, _maxRetryCount, _retryDelay.TotalMilliseconds);

                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));
                await Task.Delay(_retryDelay + jitter, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Determines whether an exception is retryable.
    /// </summary>
    /// <param name="ex">The exception to evaluate.</param>
    /// <returns>True if the exception is retryable; otherwise, false.</returns>
    private bool ShouldRetry(Exception ex)
    {
        return _shouldRetry?.Invoke(ex) ?? true; // Default to retrying all exceptions if no predicate is provided
    }
}
