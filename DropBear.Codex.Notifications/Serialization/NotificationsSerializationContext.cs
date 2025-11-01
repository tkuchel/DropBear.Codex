#region

using System.Text.Json.Serialization;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;
using DropBear.Codex.Notifications.Errors;

#endregion

namespace DropBear.Codex.Notifications.Serialization;

/// <summary>
///     JSON serialization context for DropBear.Codex.Notifications types.
///     Provides source-generated serialization for 20-30% performance improvement over reflection-based JSON serialization.
/// </summary>
/// <remarks>
///     This context includes all commonly serialized Notifications types:
///     - NotificationError for notification-specific errors
///     - Common Result types used in notification operations
///     - Notification payloads and metadata
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default,
    UseStringEnumConverter = true)]
// Notification error types
[JsonSerializable(typeof(NotificationError))]
// Common Result types for notification operations
[JsonSerializable(typeof(Result<Unit, NotificationError>))]
[JsonSerializable(typeof(Result<string, NotificationError>))]
[JsonSerializable(typeof(Result<bool, NotificationError>))]
[JsonSerializable(typeof(Result<object, NotificationError>))]
[JsonSerializable(typeof(Result<byte[], NotificationError>))]
// Core error fallbacks
[JsonSerializable(typeof(Result<Unit, SimpleError>))]
[JsonSerializable(typeof(Result<Unit, OperationError>))]
[JsonSerializable(typeof(Result<string, SimpleError>))]
// Common collections
[JsonSerializable(typeof(List<NotificationError>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class NotificationsSerializationContext : JsonSerializerContext
{
}
