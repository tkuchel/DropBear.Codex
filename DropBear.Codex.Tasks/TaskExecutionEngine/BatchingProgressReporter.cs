#region

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Handles batching of progress updates to reduce message overhead.
/// </summary>
public sealed class BatchingProgressReporter : IAsyncDisposable
{
    private const int DefaultBatchSize = 100;
    private readonly TimeSpan _batchInterval;
    private readonly Channel<TaskProgressMessage> _channel;
    private readonly Guid _channelId;
    private readonly CancellationTokenSource _cts;
    private readonly ILogger _logger;
    private readonly MessagePublisher _messagePublisher;
    private readonly SemaphoreSlim _processingSemaphore;
    private readonly Task _processingTask;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BatchingProgressReporter" /> class.
    /// </summary>
    /// <param name="messagePublisher">Publishes progress messages.</param>
    /// <param name="channelId">Identifies the channel or context for these messages.</param>
    /// <param name="batchInterval">The interval at which to flush progress updates.</param>
    public BatchingProgressReporter(
        MessagePublisher messagePublisher,
        Guid channelId,
        TimeSpan batchInterval)
    {
        _logger = LoggerFactory.Logger.ForContext<BatchingProgressReporter>();
        _messagePublisher = messagePublisher;
        _channelId = channelId;
        _batchInterval = batchInterval;
        _cts = new CancellationTokenSource();
        _processingSemaphore = new SemaphoreSlim(1, 1);

        // Use unbounded channel to prevent backpressure
        _channel = Channel.CreateUnbounded<TaskProgressMessage>(new UnboundedChannelOptions
        {
            SingleReader = true, SingleWriter = false
        });

        _processingTask = ProcessChannelAsync();
    }

    /// <summary>
    ///     Disposes the reporter, ensuring that remaining messages are flushed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            // Stop accepting new messages
            _channel.Writer.Complete();

            // Cancel ongoing processing
            await _cts.CancelAsync().ConfigureAwait(false);

            // Wait for processing to complete
            try
            {
                await _processingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }

            // Clean up resources
            _cts.Dispose();
            _processingSemaphore.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during BatchingProgressReporter disposal");
        }
    }

    /// <summary>
    ///     Queues a progress update to be batched and published.
    /// </summary>
    /// <param name="message">The progress message.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask QueueProgressUpdate(TaskProgressMessage message)
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        return _channel.Writer.WriteAsync(message);
    }

    /// <summary>
    ///     Continuously processes queued messages in batches until cancellation or disposal.
    /// </summary>
    private async Task ProcessChannelAsync()
    {
        var batch = TaskCollectionPools.RentList<TaskProgressMessage>(DefaultBatchSize);

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                // Wait for the configured interval before flushing the next batch
                await Task.Delay(_batchInterval, _cts.Token).ConfigureAwait(false);

                await _processingSemaphore.WaitAsync(_cts.Token).ConfigureAwait(false);
                try
                {
                    // Read up to DefaultBatchSize messages
                    while (batch.Count < DefaultBatchSize &&
                           _channel.Reader.TryRead(out var message))
                    {
                        batch.Add(message);
                    }

                    if (batch.Count > 0)
                    {
                        await ProcessBatchAsync(batch).ConfigureAwait(false);
                        batch.Clear();
                    }
                }
                finally
                {
                    _processingSemaphore.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Progress reporting cancelled");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing progress updates");
        }
        finally
        {
            // *** CHANGE *** Flush leftover messages if any remain after cancellation
            try
            {
                await _processingSemaphore.WaitAsync().ConfigureAwait(false);
                if (batch.Count > 0)
                {
                    await ProcessBatchAsync(batch).ConfigureAwait(false);
                    batch.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error flushing final progress batch during shutdown");
            }
            finally
            {
                _processingSemaphore.Release();
                TaskCollectionPools.Return(batch);
            }
        }
    }

    /// <summary>
    ///     Publishes the batch of progress messages via <see cref="_messagePublisher" />.
    /// </summary>
    private async Task ProcessBatchAsync(List<TaskProgressMessage> batch)
    {
        try
        {
            foreach (var message in batch)
            {
                await _messagePublisher.QueueMessage(_channelId, message).ConfigureAwait(false);
            }

            _logger.Debug("Processed batch of {Count} progress updates", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process progress update batch");
        }
    }
}
