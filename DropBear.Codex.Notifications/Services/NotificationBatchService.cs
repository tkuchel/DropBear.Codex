#region

using DropBear.Codex.Notifications.Models;
using DropBear.Codex.Serialization.Interfaces;
using MessagePipe;

#endregion

namespace DropBear.Codex.Notifications.Services;

/// <summary>
///     Provides functionality to batch notifications and publish them asynchronously.
/// </summary>
public sealed class NotificationBatchService : IAsyncDisposable
{
    private readonly IAsyncPublisher<List<byte[]>> _batchPublisher;
    private readonly List<byte[]> _batchQueue;
    private readonly int _batchSize;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ISerializer? _serializer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotificationBatchService" /> class.
    /// </summary>
    /// <param name="batchPublisher">The publisher for batched notifications.</param>
    /// <param name="batchSize">The size of each batch. Must be greater than 0.</param>
    /// <param name="serializer">Optional serializer for notifications.</param>
    /// <exception cref="ArgumentNullException">Thrown if batchPublisher is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if batchSize is less than or equal to 0.</exception>
    public NotificationBatchService(
        IAsyncPublisher<List<byte[]>> batchPublisher,
        int batchSize = 10,
        ISerializer? serializer = null)
    {
        _batchPublisher = batchPublisher ?? throw new ArgumentNullException(nameof(batchPublisher));
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0.");
        }

        _batchSize = batchSize;
        _serializer = serializer;
        _batchQueue = new List<byte[]>(_batchSize);
    }

    /// <summary>
    ///     Asynchronously releases the resources used by the NotificationBatchService.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await PublishBatchAsync().ConfigureAwait(false);
        _semaphore.Dispose();
    }

    /// <summary>
    ///     Adds a notification to the batch and publishes the batch when the size is met.
    /// </summary>
    /// <param name="notification">The notification to add to the batch.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <exception cref="ArgumentNullException">Thrown if notification is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if serialization is requested but no serializer is available.</exception>
    public async Task AddNotificationToBatchAsync(Notification notification,
        CancellationToken cancellationToken = default)
    {
        if (_serializer == null)
        {
            throw new InvalidOperationException("Serializer is not available.");
        }

        var serializedData = await _serializer.SerializeAsync(notification, cancellationToken);
        _batchQueue.Add(serializedData);

        if (_batchQueue.Count >= _batchSize)
        {
            await PublishBatchAsync(cancellationToken);
        }
    }

    /// <summary>
    ///     Publishes the current batch of notifications.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    public async Task PublishBatchAsync(CancellationToken cancellationToken = default)
    {
        List<byte[]> batchToPublish;

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_batchQueue.Count == 0)
            {
                return;
            }

            batchToPublish = new List<byte[]>(_batchQueue);
            _batchQueue.Clear();
        }
        finally
        {
            _semaphore.Release();
        }

        await _batchPublisher.PublishAsync(batchToPublish, cancellationToken).ConfigureAwait(false);
    }
}
