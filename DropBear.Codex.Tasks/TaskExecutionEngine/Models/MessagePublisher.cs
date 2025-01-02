#region

#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using MessagePipe;
using Microsoft.Extensions.ObjectPool;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

#endregion


/// <summary>
///     Manages message publishing and queuing for the execution engine.
/// </summary>
public sealed class MessagePublisher : IAsyncDisposable
{
    private readonly IAsyncPublisher<Guid, TaskCompletedMessage> _completedPublisher;
    private readonly IAsyncPublisher<Guid, TaskFailedMessage> _failedPublisher;
    private readonly ILogger _logger;
    private readonly Queue<(Guid ChannelId, object Message, Type MessageType)> _messageQueue = new();
    private readonly IAsyncPublisher<Guid, TaskProgressMessage> _progressPublisher;
    private readonly CancellationTokenSource _publisherCts = new();
    private readonly Task _publisherTask;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private readonly IAsyncPublisher<Guid, TaskStartedMessage> _startedPublisher;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the MessagePublisher class.
    /// </summary>
    public MessagePublisher(
        IAsyncPublisher<Guid, TaskProgressMessage> progressPublisher,
        IAsyncPublisher<Guid, TaskStartedMessage> startedPublisher,
        IAsyncPublisher<Guid, TaskCompletedMessage> completedPublisher,
        IAsyncPublisher<Guid, TaskFailedMessage> failedPublisher)
    {
        _logger = LoggerFactory.Logger.ForContext<MessagePublisher>();
        _progressPublisher = progressPublisher;
        _startedPublisher = startedPublisher;
        _completedPublisher = completedPublisher;
        _failedPublisher = failedPublisher;

        _publisherTask = Task.Run(PublishMessagesAsync);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Cancel the publishing task
        await _publisherCts.CancelAsync().ConfigureAwait(false);

        // Ensure pending messages are processed
        await _publishLock.WaitAsync().ConfigureAwait(false);
        try
        {
            while (_messageQueue.TryDequeue(out var message))
            {
                try
                {
                    await PublishMessageAsync(message.ChannelId, message.Message, message.MessageType)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to publish message during disposal. MessageType: {MessageType}", message.MessageType);
                }
            }
        }
        finally
        {
            _publishLock.Release();
        }

        // Wait for the publishing task to complete
        try
        {
            await _publisherTask.ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            _logger.Information("MessagePublisher disposed during cancellation.");
        }
        finally
        {
            _publishLock.Dispose();
            _publisherCts.Dispose();
        }
    }


    /// <summary>
    ///     Queues a message for publishing.
    /// </summary>
    public void QueueMessage<T>(Guid channelId, T message)
    {
        if (!_disposed)
        {
            _messageQueue.Enqueue((channelId, message!, typeof(T)));
        }
        else
        {
            throw new ObjectDisposedException(nameof(MessagePublisher));
        }
    }

    private async Task PublishMessagesAsync()
    {
        while (!_publisherCts.Token.IsCancellationRequested)
        {
            await _publishLock.WaitAsync(_publisherCts.Token).ConfigureAwait(false);
            try
            {
                while (_messageQueue.TryDequeue(out var message))
                {
                    try
                    {
                        await PublishMessageAsync(message.ChannelId, message.Message, message.MessageType)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to publish message of type {MessageType}", message.MessageType);
                    }
                }
            }
            finally
            {
                _publishLock.Release();
            }

            try
            {
                await Task.Delay(10, _publisherCts.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                _logger.Debug("Publishing loop cancelled.");
                break; // Exit loop gracefully
            }
        }
    }


    private async Task PublishMessageAsync(Guid channelId, object message, Type messageType)
    {
        if (messageType == typeof(TaskProgressMessage))
        {
            await _progressPublisher.PublishAsync(channelId, (TaskProgressMessage)message, _publisherCts.Token).ConfigureAwait(false);
        }
        else if (messageType == typeof(TaskStartedMessage))
        {
            await _startedPublisher.PublishAsync(channelId, (TaskStartedMessage)message, _publisherCts.Token).ConfigureAwait(false);
        }
        else if (messageType == typeof(TaskCompletedMessage))
        {
            await _completedPublisher.PublishAsync(channelId, (TaskCompletedMessage)message, _publisherCts.Token).ConfigureAwait(false);
        }
        else if (messageType == typeof(TaskFailedMessage))
        {
            await _failedPublisher.PublishAsync(channelId, (TaskFailedMessage)message, _publisherCts.Token).ConfigureAwait(false);
        }
    }
}
