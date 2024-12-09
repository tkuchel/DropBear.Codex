#region

using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Filters;

/// <summary>
///     Provides performance monitoring for the asynchronous handling of messages.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
public sealed class AsyncPerformanceMonitoringFilter<TMessage> : AsyncMessageHandlerFilter<TMessage>
{
    private readonly ILogger _logger;
    private readonly long _loggingThresholdMilliseconds;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncPerformanceMonitoringFilter{TMessage}" /> class.
    /// </summary>
    /// <param name="loggingThresholdMilliseconds">Optional threshold (in milliseconds) for logging performance data.</param>
    public AsyncPerformanceMonitoringFilter(long loggingThresholdMilliseconds = 0)
    {
        if (loggingThresholdMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(loggingThresholdMilliseconds),
                "Threshold cannot be negative.");
        }

        _logger = LoggerFactory.Logger.ForContext(typeof(TMessage));
        _loggingThresholdMilliseconds = loggingThresholdMilliseconds;
    }

    /// <summary>
    ///     Measures and logs the time taken to handle a message.
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
        var stopwatch = Stopwatch.StartNew();
        var messageDetails = GetMessageDetails(message);

        try
        {
            await next(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex,
                "Error while handling message of type {MessageType} after {ElapsedMilliseconds} ms: {MessageDetails}",
                typeof(TMessage).Name, stopwatch.ElapsedMilliseconds, messageDetails);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds >= _loggingThresholdMilliseconds)
            {
                _logger.Information(
                    "Handled message of type {MessageType} in {ElapsedMilliseconds} ms: {MessageDetails}",
                    typeof(TMessage).Name, stopwatch.ElapsedMilliseconds, messageDetails);
            }
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
