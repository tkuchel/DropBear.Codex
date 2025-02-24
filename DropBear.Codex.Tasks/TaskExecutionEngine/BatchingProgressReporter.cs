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
///     Handles batching of progress updates to reduce message overhead
/// </summary>
public sealed class BatchingProgressReporter : IAsyncDisposable
{
    private const int DefaultBatchSize = 100;
    private readonly Channel<TaskProgressMessage> _channel;
    private readonly TimeSpan _batchInterval;
    private readonly Guid _channelId;
    private readonly CancellationTokenSource _cts;
    private readonly ILogger _logger;
    private readonly MessagePublisher _messagePublisher;
    private readonly Task _processingTask;
    private readonly SemaphoreSlim _processingSemaphore;
    private bool _disposed;

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
            SingleReader = true,
            SingleWriter = false
        });

        _processingTask = ProcessChannelAsync();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask QueueProgressUpdate(TaskProgressMessage message)
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        return _channel.Writer.WriteAsync(message);
    }

    private async Task ProcessChannelAsync()
    {
        var batch = TaskCollectionPools.RentList<TaskProgressMessage>(DefaultBatchSize);

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(_batchInterval, _cts.Token).ConfigureAwait(false);

                await _processingSemaphore.WaitAsync(_cts.Token).ConfigureAwait(false);
                try
                {
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
            TaskCollectionPools.Return(batch);
        }
    }

    private async Task ProcessBatchAsync(List<TaskProgressMessage>? batch)
    {
        try
        {
            if (batch != null)
            {
                foreach (var message in batch)
                {
                    await _messagePublisher.QueueMessage(_channelId, message).ConfigureAwait(false);
                }

                _logger.Debug("Processed batch of {Count} progress updates", batch.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process progress update batch");
        }
    }

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
}
