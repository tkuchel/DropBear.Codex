#region

using DropBear.Codex.Notifications.Models;

#endregion

namespace DropBear.Codex.Notifications.Interfaces;

public interface INotificationSerializationService
{
    Task<byte[]> SerializeNotificationAsync(Notification notification, CancellationToken cancellationToken);
    Task<Notification?> DeserializeNotificationAsync(byte[] data, CancellationToken cancellationToken);
    Task PersistNotificationAsync(Notification notification, CancellationToken cancellationToken);
}
