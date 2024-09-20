#region

using System.Text;
using System.Text.Json;
using DropBear.Codex.Notifications.Exceptions;

#endregion

namespace DropBear.Codex.Notifications.Services;

/// <summary>
///     Provides functionality to persist notifications to a file.
/// </summary>
public sealed class NotificationPersistenceService : IDisposable
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;
    private FileStream? _fileStream;
    private StreamWriter? _streamWriter;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotificationPersistenceService" /> class.
    /// </summary>
    /// <param name="filePath">The file path where notifications will be persisted.</param>
    /// <exception cref="ArgumentNullException">Thrown if filePath is null or empty.</exception>
    public NotificationPersistenceService(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        _filePath = filePath;
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        InitializeFileStream();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void InitializeFileStream()
    {
        try
        {
            _fileStream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            _streamWriter = new StreamWriter(_fileStream, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            throw new NotificationPersistenceException("Failed to initialize file stream.", ex);
        }
    }

    /// <summary>
    ///     Saves a notification to the persistence layer.
    /// </summary>
    /// <param name="notification">The notification to save.</param>
    /// <exception cref="ArgumentNullException">Thrown if notification is null.</exception>
    /// <exception cref="NotificationPersistenceException">Thrown if saving the notification fails.</exception>
    public async Task SaveNotificationAsync(Notification notification)
    {
        if (notification == null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        try
        {
            var json = JsonSerializer.Serialize(notification, _jsonOptions);
            await _streamWriter!.WriteLineAsync(json).ConfigureAwait(false);
            await _streamWriter.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new NotificationPersistenceException("Failed to save notification.", ex);
        }
    }

    /// <summary>
    ///     Saves a serialized notification to the persistence layer.
    /// </summary>
    /// <param name="serializedNotification">The serialized notification to save.</param>
    /// <exception cref="ArgumentNullException">Thrown if serializedNotification is null.</exception>
    /// <exception cref="NotificationPersistenceException">Thrown if saving the serialized notification fails.</exception>
    public async Task SaveSerializedNotificationAsync(byte[] serializedNotification)
    {
        if (serializedNotification == null)
        {
            throw new ArgumentNullException(nameof(serializedNotification));
        }

        try
        {
            var base64Message = Convert.ToBase64String(serializedNotification);
            await _streamWriter!.WriteLineAsync(base64Message).ConfigureAwait(false);
            await _streamWriter.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new NotificationPersistenceException("Failed to save serialized notification.", ex);
        }
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _streamWriter?.Dispose();
            _fileStream?.Dispose();
        }

        _disposed = true;
    }
}
