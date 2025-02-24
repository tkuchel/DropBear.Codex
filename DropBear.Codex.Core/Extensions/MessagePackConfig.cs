#region

using MessagePack;
using MessagePack.Resolvers;

#endregion

namespace DropBear.Codex.Core.Extensions;

/// <summary>
///     Provides centralized configuration for MessagePack serialization across the DropBear.Codex.Core library.
/// </summary>
public static class MessagePackConfig
{
    /// <summary>
    ///     Configures default MessagePack serialization options with recommended settings.
    /// </summary>
    public static void Initialize()
    {
        MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4BlockArray)
            .WithResolver(StandardResolverAllowPrivate.Instance)
            .WithSecurity(MessagePackSecurity.UntrustedData);
    }
}
