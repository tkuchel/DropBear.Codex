#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Interfaces;
using DropBear.Codex.Serialization.Serializers;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Factories;

[SupportedOSPlatform("windows")]
public abstract class SerializerFactory
{
    private static readonly ILogger _logger = LoggerFactory.Logger.ForContext<SerializerFactory>();

    public static ISerializer CreateSerializer(SerializationConfig config)
    {
        _ = config ?? throw new ArgumentNullException(nameof(config), "Configuration must be provided.");

        _logger.Information("Starting serializer creation.");

        ValidateConfiguration(config);

        var serializer = CreateBaseSerializer(config);
        serializer = ApplyCompression(config, serializer);
        serializer = ApplyEncryption(config, serializer);
        serializer = ApplyEncoding(config, serializer);

        _logger.Information("Serializer creation completed successfully.");
        return serializer;
    }

    private static void ValidateConfiguration(SerializationConfig config)
    {
        if (config.SerializerType == null)
        {
            var message = "Serializer type must be specified.";
            _logger.Error(message);
            throw new ArgumentException(message, nameof(config.SerializerType));
        }

        if (config.RecyclableMemoryStreamManager is null)
        {
            var message = "RecyclableMemoryStreamManager must be specified.";
            _logger.Error(message);
            throw new ArgumentException(message, nameof(config.RecyclableMemoryStreamManager));
        }

        _logger.Information("Configuration validated successfully.");
    }

    private static ISerializer CreateBaseSerializer(SerializationConfig config)
    {
        var serializerType = config.SerializerType ?? throw new InvalidOperationException("Serializer type not set.");
        _logger.Information($"Creating base serializer of type {serializerType.Name}.");

        return InstantiateSerializer(config, serializerType);
    }

    private static ISerializer InstantiateSerializer(SerializationConfig config, Type serializerType)
    {
        var constructor = serializerType.GetConstructor(new[] { typeof(SerializationConfig) })
                          ?? throw new InvalidOperationException(
                              $"No suitable constructor found for {serializerType.FullName}.");

        _logger.Information($"Instantiating serializer of type {serializerType.Name}.");

        return (ISerializer)constructor.Invoke(new object[] { config });
    }

    private static ISerializer ApplyCompression(SerializationConfig config, ISerializer serializer)
    {
        if (config.CompressionProviderType == null)
        {
            _logger.Information("No compression provider configured.");
            return serializer;
        }

        var compressor = (ICompressionProvider)CreateProvider(config, config.CompressionProviderType);
        _logger.Information($"Applying compression provider of type {config.CompressionProviderType.Name}.");

        return new CompressedSerializer(serializer, compressor);
    }

    private static ISerializer ApplyEncryption(SerializationConfig config, ISerializer serializer)
    {
        if (config.EncryptionProviderType == null)
        {
            _logger.Information("No encryption provider configured.");
            return serializer;
        }

        var encryptor = (IEncryptionProvider)CreateProvider(config, config.EncryptionProviderType);
        _logger.Information($"Applying encryption provider of type {config.EncryptionProviderType.Name}.");

        return new EncryptedSerializer(serializer, encryptor);
    }

    private static ISerializer ApplyEncoding(SerializationConfig config, ISerializer serializer)
    {
        if (config.EncodingProviderType == null)
        {
            _logger.Information("No encoding provider configured.");
            return serializer;
        }

        var encoder = (IEncodingProvider)CreateProvider(config, config.EncodingProviderType);
        _logger.Information($"Applying encoding provider of type {config.EncodingProviderType.Name}.");

        return new EncodedSerializer(serializer, encoder);
    }

    private static object CreateProvider(SerializationConfig config, Type providerType)
    {
        var constructor = providerType.GetConstructor(new[] { typeof(SerializationConfig) })
                          ?? throw new InvalidOperationException(
                              $"No suitable constructor found for {providerType.FullName}.");

        _logger.Information($"Instantiating provider of type {providerType.Name}.");

        return constructor.Invoke(new object[] { config });
    }
}
