#region

using System.Collections.Concurrent;
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
    private readonly TimeSpan _batchInterval;
    private readonly Guid _channelId;
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger _logger;
    private readonly MessagePublisher _messagePublisher;
    private readonly Task _processingTask;
    private readonly ConcurrentQueue<TaskProgressMessage> _queue = new();

    public BatchingProgressReporter(
        MessagePublisher messagePublisher,
        Guid channelId,
        TimeSpan batchInterval)
    {
        _logger = LoggerFactory.Logger.ForContext<BatchingProgressReporter>();
        _messagePublisher = messagePublisher;
        _channelId = channelId;
        _batchInterval = batchInterval;
        _processingTask = ProcessQueueAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        await _processingTask.ConfigureAwait(false);
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    public void QueueProgressUpdate(TaskProgressMessage message)
    {
        _queue.Enqueue(message);
    }

    private async Task ProcessQueueAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_batchInterval, _cts.Token).ConfigureAwait(false);
                await ProcessBatchAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing progress update batch");
            }
        }
    }

    private async Task ProcessBatchAsync()
    {
        var batch = new List<TaskProgressMessage>();
        while (_queue.TryDequeue(out var message))
        {
            batch.Add(message);
        }

        if (batch.Count == 0)
        {
            return;
        }

        foreach (var message in batch)
        {
            _messagePublisher.QueueMessage(_channelId, message);
        }

        _logger.Debug("Processed batch of {Count} progress updates", batch.Count);
    }
}
