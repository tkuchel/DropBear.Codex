#region

using DropBear.Codex.Core.Logging;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

/// <summary>
///     Ensures that only a limited number of messages are processed concurrently.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
public sealed class AsyncThrottlingFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>, IDisposable
{
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncThrottlingFilter{TMessage}" /> class.
    /// </summary>
    /// <param name="maxConcurrentMessages">The maximum number of messages to process concurrently.</param>
    public AsyncThrottlingFilter(int maxConcurrentMessages)
    {
        if (maxConcurrentMessages <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentMessages), "Must be greater than zero.");
        }

        _semaphore = new SemaphoreSlim(maxConcurrentMessages, maxConcurrentMessages);
        _logger = LoggerFactory.Logger.ForContext(typeof(TMessage));
    }

    /// <summary>
    ///     Disposes the resources used by the throttling filter.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _semaphore.Dispose();
        _disposed = true;
    }

    /// <summary>
    ///     Handles messages with concurrency throttling.
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
        var messageDetails = GetMessageDetails(message);

        try
        {
            _logger.Debug("Waiting to process message of type {MessageType}: {MessageDetails}",
                typeof(TMessage).Name, messageDetails);

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            _logger.Information("Processing message of type {MessageType}: {MessageDetails}",
                typeof(TMessage).Name, messageDetails);

            await next(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Message processing was cancelled for message type {MessageType}: {MessageDetails}",
                typeof(TMessage).Name, messageDetails);
            throw;
        }
        finally
        {
            _semaphore.Release();
            _logger.Debug("Finished processing message of type {MessageType}: {MessageDetails}",
                typeof(TMessage).Name, messageDetails);
        }
    }

    /// <summary>
    ///     Extracts a meaningful representation of the message for logging purposes.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <returns>A string representation of the message.</returns>
    private static string GetMessageDetails(TMessage message)
    {
        try
        {
            return message?.ToString() ?? "null";
        }
        catch
        {
            return "Unable to retrieve message details.";
        }
    }
}
