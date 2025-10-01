#region

using System.Diagnostics;
using System.Runtime.Serialization;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Exceptions;

/// <summary>
///     Exception thrown during MessagePack serialization operations.
///     Optimized for .NET 9 with enhanced diagnostics.
/// </summary>
[DebuggerDisplay("MessagePackSerializationException: {Message}")]
[Serializable]
public sealed class MessagePackSerializationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of MessagePackSerializationException.
    /// </summary>
    public MessagePackSerializationException()
        : base("An error occurred during MessagePack serialization or deserialization.")
    {
        Timestamp = DateTime.UtcNow;
        ActivityId = Activity.Current?.Id;
    }

    /// <summary>
    ///     Initializes a new instance with a message.
    /// </summary>
    public MessagePackSerializationException(string message)
        : base(message)
    {
        Timestamp = DateTime.UtcNow;
        ActivityId = Activity.Current?.Id;
    }

    /// <summary>
    ///     Initializes a new instance with a message and inner exception.
    /// </summary>
    public MessagePackSerializationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Timestamp = DateTime.UtcNow;
        ActivityId = Activity.Current?.Id;
    }

#if NET8_0_OR_GREATER
    [Obsolete("This API supports obsolete formatter-based serialization.", DiagnosticId = "SYSLIB0051")]
#endif
    private MessagePackSerializationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        Timestamp = info.GetDateTime(nameof(Timestamp));
        ActivityId = info.GetString(nameof(ActivityId));
        TypeBeingSerialized = info.GetString(nameof(TypeBeingSerialized));
        Operation = info.GetString(nameof(Operation));
    }

    /// <summary>
    ///     Gets the timestamp when this exception occurred.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    ///     Gets the Activity ID for distributed tracing.
    /// </summary>
    public string? ActivityId { get; init; }

    /// <summary>
    ///     Gets the type being serialized/deserialized.
    /// </summary>
    public string? TypeBeingSerialized { get; init; }

    /// <summary>
    ///     Gets the operation that failed (Serialize or Deserialize).
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    ///     Creates an exception for a serialization failure.
    /// </summary>
    public static MessagePackSerializationException ForSerialization<T>(Exception innerException)
    {
        return new MessagePackSerializationException(
            $"Failed to serialize type {typeof(T).Name} to MessagePack",
            innerException)
        {
            TypeBeingSerialized = typeof(T).FullName,
            Operation = "Serialize"
        };
    }

    /// <summary>
    ///     Creates an exception for a deserialization failure.
    /// </summary>
    public static MessagePackSerializationException ForDeserialization<T>(Exception innerException)
    {
        return new MessagePackSerializationException(
            $"Failed to deserialize MessagePack data to type {typeof(T).Name}",
            innerException)
        {
            TypeBeingSerialized = typeof(T).FullName,
            Operation = "Deserialize"
        };
    }

#if NET8_0_OR_GREATER
    [Obsolete("This API supports obsolete formatter-based serialization.", DiagnosticId = "SYSLIB0051")]
#endif
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        ArgumentNullException.ThrowIfNull(info);

        base.GetObjectData(info, context);
        info.AddValue(nameof(Timestamp), Timestamp);
        info.AddValue(nameof(ActivityId), ActivityId);
        info.AddValue(nameof(TypeBeingSerialized), TypeBeingSerialized);
        info.AddValue(nameof(Operation), Operation);
    }
}
