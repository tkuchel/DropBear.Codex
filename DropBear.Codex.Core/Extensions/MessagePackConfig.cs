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
    // Default options - cached for performance
    private static readonly MessagePackSerializerOptions DefaultOptions =
        MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4BlockArray)
            .WithResolver(StandardResolverAllowPrivate.Instance)
            .WithSecurity(MessagePackSecurity.UntrustedData);

    // High-performance options - no compression
    private static readonly MessagePackSerializerOptions HighPerformanceOptions =
        MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.None)
            .WithResolver(StandardResolverAllowPrivate.Instance)
            .WithSecurity(MessagePackSecurity.UntrustedData);

    // Compact options - maximum compression
    private static readonly MessagePackSerializerOptions CompactOptions =
        MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4Block)
            .WithResolver(StandardResolverAllowPrivate.Instance)
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

/// <summary>
///     Builder for creating custom MessagePack configurations.
/// </summary>
public sealed class MessagePackConfigBuilder
{
    private bool _allowAssemblyVersionMismatch;
    private MessagePackCompression _compression = MessagePackCompression.Lz4BlockArray;
    private bool _omitAssemblyVersion;
    private IFormatterResolver _resolver = StandardResolverAllowPrivate.Instance;
    private MessagePackSecurity _security = MessagePackSecurity.UntrustedData;

    internal MessagePackConfigBuilder()
    {
    }

    /// <summary>
    ///     Sets the compression mode.
    /// </summary>
    public MessagePackConfigBuilder WithCompression(MessagePackCompression compression)
    {
        _compression = compression;
        return this;
    }

    /// <summary>
    ///     Disables compression for maximum performance.
    /// </summary>
    public MessagePackConfigBuilder WithoutCompression()
    {
        _compression = MessagePackCompression.None;
        return this;
    }

    /// <summary>
    ///     Uses LZ4 block compression for maximum compactness.
    /// </summary>
    public MessagePackConfigBuilder WithMaximumCompression()
    {
        _compression = MessagePackCompression.Lz4Block;
        return this;
    }

    /// <summary>
    ///     Sets the formatter resolver.
    /// </summary>
    public MessagePackConfigBuilder WithResolver(IFormatterResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
        return this;
    }

    /// <summary>
    ///     Uses the standard resolver (public members only).
    /// </summary>
    public MessagePackConfigBuilder WithStandardResolver()
    {
        _resolver = StandardResolver.Instance;
        return this;
    }

    /// <summary>
    ///     Uses the standard resolver that allows private members.
    /// </summary>
    public MessagePackConfigBuilder WithPrivateMemberResolver()
    {
        _resolver = StandardResolverAllowPrivate.Instance;
        return this;
    }

    /// <summary>
    ///     Uses the contract-less resolver (allows dynamic serialization).
    /// </summary>
    public MessagePackConfigBuilder WithContractlessResolver()
    {
        _resolver = ContractlessStandardResolver.Instance;
        return this;
    }

    /// <summary>
    ///     Sets the security mode.
    /// </summary>
    public MessagePackConfigBuilder WithSecurity(MessagePackSecurity security)
    {
        _security = security;
        return this;
    }

    /// <summary>
    ///     Configures for trusted data (better performance, less safe).
    /// </summary>
    public MessagePackConfigBuilder WithTrustedData()
    {
        _security = MessagePackSecurity.TrustedData;
        return this;
    }

    /// <summary>
    ///     Configures for untrusted data (safer, some overhead).
    /// </summary>
    public MessagePackConfigBuilder WithUntrustedData()
    {
        _security = MessagePackSecurity.UntrustedData;
        return this;
    }

    /// <summary>
    ///     Omits assembly version from serialized data.
    /// </summary>
    public MessagePackConfigBuilder OmitAssemblyVersion()
    {
        _omitAssemblyVersion = true;
        return this;
    }

    /// <summary>
    ///     Allows assembly version mismatches during deserialization.
    /// </summary>
    public MessagePackConfigBuilder AllowAssemblyVersionMismatch()
    {
        _allowAssemblyVersionMismatch = true;
        return this;
    }

    /// <summary>
    ///     Builds the MessagePackSerializerOptions.
    /// </summary>
    public MessagePackSerializerOptions Build()
    {
        var options = MessagePackSerializerOptions.Standard
            .WithCompression(_compression)
            .WithResolver(_resolver)
            .WithSecurity(_security);

        if (_omitAssemblyVersion)
        {
            options = options.WithOmitAssemblyVersion(true);
        }

        if (_allowAssemblyVersionMismatch)
        {
            options = options.WithAllowAssemblyVersionMismatch(true);
        }

        return options;
    }
}
