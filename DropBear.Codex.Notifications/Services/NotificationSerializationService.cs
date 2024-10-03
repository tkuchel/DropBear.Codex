#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Models;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Notifications.Services;

public class NotificationSerializationService : INotificationSerializationService
{
    private readonly NotificationPersistenceService _persistenceService;
    private readonly ISerializer _serializer;
    private readonly ILogger _logger;

    public NotificationSerializationService(ISerializer serializer, NotificationPersistenceService persistenceService)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        _logger = LoggerFactory.Logger.ForContext<GlobalNotificationService>();
    }

    public async Task<byte[]> SerializeNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        return await _serializer.SerializeAsync(notification, cancellationToken);
    }

    public async Task<Notification?> DeserializeNotificationAsync(byte[] data, CancellationToken cancellationToken)
    {
        return await _serializer.DeserializeAsync<Notification>(data, cancellationToken);
    }

    public async Task PersistNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        var data = await _serializer.SerializeAsync(notification, cancellationToken);
        await _persistenceService.SaveSerializedNotificationAsync(data);
    }
}
