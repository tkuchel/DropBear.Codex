using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DropBear.Codex.Notifications.Enums;

namespace DropBear.Codex.Notifications
{
    /// <summary>
    /// Represents a notification in the DropBear Codex ecosystem.
    /// </summary>
    public class Notification
    {
        private readonly Dictionary<string, object> _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="Notification"/> class.
        /// </summary>
        /// <param name="type">The type of the alert.</param>
        /// <param name="message">The message content of the notification.</param>
        /// <param name="severity">The severity level of the notification.</param>
        /// <param name="data">Additional data associated with the notification (optional).</param>
        /// <exception cref="ArgumentNullException">Thrown if message is null or empty.</exception>
        public Notification(AlertType type, string message, NotificationSeverity severity,
            Dictionary<string, object>? data = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentNullException(nameof(message), "Message cannot be null or empty.");
            }

            Type = type;
            Message = message;
            Timestamp = DateTime.UtcNow;
            Severity = severity;
            _data = data ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets the type of the alert.
        /// </summary>
        public AlertType Type { get; }

        /// <summary>
        /// Gets the message content of the notification.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the UTC timestamp when the notification was created.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the severity level of the notification.
        /// </summary>
        public NotificationSeverity Severity { get; }

        /// <summary>
        /// Gets a read-only dictionary of additional data associated with the notification.
        /// </summary>
        public IReadOnlyDictionary<string, object> Data => new ReadOnlyDictionary<string, object>(_data);

        /// <summary>
        /// Adds or updates a key-value pair in the notification's data dictionary.
        /// </summary>
        /// <param name="key">The key of the element to add or update.</param>
        /// <param name="value">The value of the element to add or update.</param>
        /// <exception cref="ArgumentNullException">Thrown if key is null.</exception>
        public void AddOrUpdateData(string key, object value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _data[key] = value;
        }

        /// <summary>
        /// Removes a key-value pair from the notification's data dictionary.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>true if the element is successfully found and removed; otherwise, false.</returns>
        public bool RemoveData(string key)
        {
            return _data.Remove(key);
        }
    }
}
