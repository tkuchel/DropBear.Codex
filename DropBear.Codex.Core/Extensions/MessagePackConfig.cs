#region

using MessagePack;
using MessagePack.Resolvers;

#endregion

namespace DropBear.Codex.Core.Extensions;

/// <summary>
///     Provides centralized configuration for MessagePack serialization.
///     Optimized for .NET 9 with modern patterns and builder support.
/// </summary>
public static class MessagePackConfig
{
    private static readonly IFormatterResolver SafeDefaultResolver = ContractlessStandardResolver.Instance;

    // Default options - cached for performance
    private static readonly MessagePackSerializerOptions DefaultOptions =
        MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4BlockArray)
            .WithResolver(SafeDefaultResolver)
            .WithSecurity(MessagePackSecurity.UntrustedData);

    // High-performance options - no compression
    private static readonly MessagePackSerializerOptions HighPerformanceOptions =
        MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.None)
            .WithResolver(SafeDefaultResolver)
            .WithSecurity(MessagePackSecurity.UntrustedData);

    // Compact options - maximum compression
    private static readonly MessagePackSerializerOptions CompactOptions =
        MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4Block)
            .WithResolver(SafeDefaultResolver)
            .WithSecurity(MessagePackSecurity.UntrustedData);

    /// <summary>
    ///     Gets the default MessagePack serializer options.
    /// </summary>
    /// <returns>MessagePackSerializerOptions with standard settings.</returns>
    public static MessagePackSerializerOptions GetOptions() => DefaultOptions;

    /// <summary>
    ///     Gets high-performance options (no compression).
    /// </summary>
    /// <returns>MessagePackSerializerOptions optimized for speed.</returns>
    public static MessagePackSerializerOptions GetHighPerformanceOptions() => HighPerformanceOptions;

    /// <summary>
    ///     Gets compact options (maximum compression).
    /// </summary>
    /// <returns>MessagePackSerializerOptions optimized for size.</returns>
    public static MessagePackSerializerOptions GetCompactOptions() => CompactOptions;

    /// <summary>
    ///     Initializes the global default options.
    ///     Call this once at application startup if you want to set global defaults.
    /// </summary>
    public static void InitializeGlobalDefaults() => MessagePackSerializer.DefaultOptions = DefaultOptions;

    /// <summary>
    ///     Creates a builder for custom configurations.
    /// </summary>
    /// <returns>A new MessagePackConfigBuilder.</returns>
    public static MessagePackConfigBuilder CreateBuilder() => new();
}
