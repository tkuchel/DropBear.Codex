#region

using System.Runtime.Versioning;
using System.Text.Json;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Factories;
using DropBear.Codex.Serialization.Interfaces;
using MessagePack;
using MessagePack.Resolvers;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Extensions;

[SupportedOSPlatform("windows")]
public static class SerializerFactoryExtensions
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(SerializerFactoryExtensions));

    public static SerializationBuilder WithDefaultJsonOptions(this SerializationBuilder builder,
        bool writeIndented = false)
    {
        Logger.Information("Configuring JSON serialization with WriteIndented: {WriteIndented}", writeIndented);
        var options = new JsonSerializerOptions { WriteIndented = writeIndented };
        return builder.WithJsonSerializerOptions(options);
    }

    public static SerializationBuilder WithDefaultMessagePackOptions(this SerializationBuilder builder,
        bool resolverEnabled = true)
    {
        Logger.Information("Configuring MessagePack serialization with ResolverEnabled: {ResolverEnabled}",
            resolverEnabled);
        var options = MessagePackSerializerOptions.Standard;
        if (resolverEnabled)
        {
            options = options.WithResolver(StandardResolver.Instance);
        }

        return builder.WithMessagePackSerializerOptions(options);
    }

    public static SerializationBuilder WithDynamicCompression(this SerializationBuilder builder,
        Func<Type> providerTypeSelector)
    {
        var providerType = providerTypeSelector();
        Logger.Information("Selecting compression provider type: {ProviderType}", providerType.Name);

        if (!typeof(ICompressionProvider).IsAssignableFrom(providerType))
        {
            var errorMessage = $"Selected type {providerType.Name} does not implement ICompressionProvider.";
            Logger.Error(errorMessage);
            throw new ArgumentException(errorMessage, nameof(providerTypeSelector));
        }

        return builder.WithCompression(providerType);
    }

    public static SerializationBuilder WithDynamicEncryption(this SerializationBuilder builder,
        Func<Type> providerTypeSelector)
    {
        var providerType = providerTypeSelector();
        Logger.Information("Selecting encryption provider type: {ProviderType}", providerType.Name);

        if (!typeof(IEncryptionProvider).IsAssignableFrom(providerType))
        {
            var errorMessage = $"Selected type {providerType.Name} does not implement IEncryptionProvider.";
            Logger.Error(errorMessage);
            throw new ArgumentException(errorMessage, nameof(providerTypeSelector));
        }

        return builder.WithEncryption(providerType);
    }

    public static bool ValidateConfiguration(this SerializationConfig config)
    {
        Logger.Information("Validating serialization configuration.");
        var isValid = config.SerializerType is not null && config.EncodingProviderType is not null &&
                      (config.CompressionProviderType is not null || config.EncryptionProviderType is not null);

        if (!isValid)
        {
            Logger.Warning(
                "Serialization configuration is invalid. SerializerType: {SerializerType}, EncodingProviderType: {EncodingProviderType}, CompressionProviderType: {CompressionProviderType}, EncryptionProviderType: {EncryptionProviderType}",
                config.SerializerType, config.EncodingProviderType, config.CompressionProviderType,
                config.EncryptionProviderType);
        }

        return isValid;
    }
}
