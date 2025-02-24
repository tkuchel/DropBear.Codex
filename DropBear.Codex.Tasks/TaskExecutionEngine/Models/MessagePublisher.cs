#region

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Manages message publishing for tasks, using a channel and an internal background loop to process messages.
/// </summary>
public sealed class MessagePublisher : IAsyncDisposable
{
    private readonly Channel<(Guid ChannelId, object Message, Type MessageType)> _channel;
    private readonly IAsyncPublisher<Guid, TaskCompletedMessage> _completedPublisher;
    private readonly IAsyncPublisher<Guid, TaskFailedMessage> _failedPublisher;
    private readonly ILogger _logger;
    private readonly IAsyncPublisher<Guid, TaskProgressMessage> _progressPublisher;
    private readonly CancellationTokenSource _publisherCts;
    private readonly Task _publisherTask;
    private readonly SemaphoreSlim _publishLock;
    private readonly IAsyncPublisher<Guid, TaskStartedMessage> _startedPublisher;
    private bool _disposed;

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

        _publisherCts = new CancellationTokenSource();
        _publishLock = new SemaphoreSlim(1, 1);

        _channel = Channel.CreateUnbounded<(Guid, object, Type)>(new UnboundedChannelOptions
        {
            SingleReader = true, SingleWriter = false
        });

        // Start background processing
        _publisherTask = ProcessMessagesAsync();
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
            // Complete channel and cancel any pending operations
            _channel.Writer.Complete();
            await _publisherCts.CancelAsync().ConfigureAwait(false);

            try
            {
                await _publisherTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }

            _publisherCts.Dispose();
            _publishLock.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during MessagePublisher disposal");
        }
    }

    /// <summary>
    ///     Queues a message for asynchronous publishing.
    /// </summary>
    /// <typeparam name="T">The message type (e.g., <see cref="TaskProgressMessage" />)</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask QueueMessage<T>(Guid channelId, T message)
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        return _channel.Writer.WriteAsync((channelId, message!, typeof(T)));
    }

    private async Task ProcessMessagesAsync()
    {
        try
        {
            while (!_publisherCts.Token.IsCancellationRequested)
            {
                var (channelId, message, messageType) =
                    await _channel.Reader.ReadAsync(_publisherCts.Token).ConfigureAwait(false);

                await _publishLock.WaitAsync(_publisherCts.Token).ConfigureAwait(false);
                try
                {
                    await PublishMessageAsync(channelId, message, messageType).ConfigureAwait(false);
                }
                finally
                {
                    _publishLock.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Message publishing cancelled");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing messages");
        }
    }

    private async Task PublishMessageAsync(Guid channelId, object message, Type messageType)
    {
        try
        {
            if (messageType == typeof(TaskProgressMessage))
            {
                await _progressPublisher.PublishAsync(channelId, (TaskProgressMessage)message, _publisherCts.Token)
                    .ConfigureAwait(false);
            }
            else if (messageType == typeof(TaskStartedMessage))
            {
                await _startedPublisher.PublishAsync(channelId, (TaskStartedMessage)message, _publisherCts.Token)
                    .ConfigureAwait(false);
            }
            else if (messageType == typeof(TaskCompletedMessage))
            {
                await _completedPublisher.PublishAsync(channelId, (TaskCompletedMessage)message, _publisherCts.Token)
                    .ConfigureAwait(false);
            }
            else if (messageType == typeof(TaskFailedMessage))
            {
                await _failedPublisher.PublishAsync(channelId, (TaskFailedMessage)message, _publisherCts.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to publish message of type {MessageType}", messageType.Name);
        }
    }
}
