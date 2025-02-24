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
    // Cache a single instance of the options so that all serializers use the same settings.
    private static readonly MessagePackSerializerOptions _options =
        MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4BlockArray)
            .WithResolver(StandardResolverAllowPrivate.Instance)
            .WithSecurity(MessagePackSecurity.UntrustedData);

    /// <summary>
    ///     Returns the custom <see cref="MessagePackSerializerOptions" /> for this library.
    /// </summary>
    public static MessagePackSerializerOptions GetOptions()
    {
        return _options;
    }

    /// <summary>
    ///     (Optional) If you want to globally set the default once, you could still call this,
    ///     but it's usually better to just use <see cref="GetOptions" /> directly in your serializers.
    /// </summary>
    public static void InitializeGlobalDefaults()
    {
        MessagePackSerializer.DefaultOptions = _options;
    }
}
