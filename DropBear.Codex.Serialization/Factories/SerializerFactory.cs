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
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<SerializerFactory>();

    public static ISerializer CreateSerializer(SerializationConfig config)
    {
        _ = config ?? throw new ArgumentNullException(nameof(config), "Configuration must be provided.");

        Logger.Information("Starting serializer creation.");

        ValidateConfiguration(config);

        var serializer = CreateBaseSerializer(config);
        serializer = ApplyCompression(config, serializer);
        serializer = ApplyEncryption(config, serializer);
        serializer = ApplyEncoding(config, serializer);

        Logger.Information("Serializer creation completed successfully.");
        return serializer;
    }

    private static void ValidateConfiguration(SerializationConfig config)
    {
        if (config.SerializerType == null)
        {
            var message = "Serializer type must be specified.";
            Logger.Error(message);
#pragma warning disable MA0015
            throw new ArgumentException(message, nameof(config.SerializerType));
#pragma warning restore MA0015
        }

        if (config.RecyclableMemoryStreamManager is null)
        {
            var message = "RecyclableMemoryStreamManager must be specified.";
            Logger.Error(message);
#pragma warning disable MA0015
            throw new ArgumentException(message, nameof(config.RecyclableMemoryStreamManager));
#pragma warning restore MA0015
        }

        Logger.Information("Configuration validated successfully.");
    }

    private static ISerializer CreateBaseSerializer(SerializationConfig config)
    {
        var serializerType = config.SerializerType ?? throw new InvalidOperationException("Serializer type not set.");
        Logger.Information($"Creating base serializer of type {serializerType.Name}.");

        return InstantiateSerializer(config, serializerType);
    }

    private static ISerializer InstantiateSerializer(SerializationConfig config, Type serializerType)
    {
        var constructor = serializerType.GetConstructor([typeof(SerializationConfig)])
                          ?? throw new InvalidOperationException(
                              $"No suitable constructor found for {serializerType.FullName}.");

        Logger.Information($"Instantiating serializer of type {serializerType.Name}.");

        return (ISerializer)constructor.Invoke([config]);
    }

    private static ISerializer ApplyCompression(SerializationConfig config, ISerializer serializer)
    {
        if (config.CompressionProviderType == null)
        {
            Logger.Information("No compression provider configured.");
            return serializer;
        }

        var compressor = (ICompressionProvider)CreateProvider(config, config.CompressionProviderType);
        Logger.Information($"Applying compression provider of type {config.CompressionProviderType.Name}.");

        return new CompressedSerializer(serializer, compressor);
    }

    private static ISerializer ApplyEncryption(SerializationConfig config, ISerializer serializer)
    {
        if (config.EncryptionProviderType == null)
        {
            Logger.Information("No encryption provider configured.");
            return serializer;
        }

        var encryptor = (IEncryptionProvider)CreateProvider(config, config.EncryptionProviderType);
        Logger.Information($"Applying encryption provider of type {config.EncryptionProviderType.Name}.");

        return new EncryptedSerializer(serializer, encryptor);
    }

    private static ISerializer ApplyEncoding(SerializationConfig config, ISerializer serializer)
    {
        if (config.EncodingProviderType == null)
        {
            Logger.Information("No encoding provider configured.");
            return serializer;
        }

        var encoder = (IEncodingProvider)CreateProvider(config, config.EncodingProviderType);
        Logger.Information($"Applying encoding provider of type {config.EncodingProviderType.Name}.");

        return new EncodedSerializer(serializer, encoder);
    }

    private static object CreateProvider(SerializationConfig config, Type providerType)
    {
        // Try to find a constructor that takes a SerializationConfig
        var constructor = providerType.GetConstructor([typeof(SerializationConfig)]);

        // If no such constructor exists, look for a parameterless constructor
        if (constructor == null)
        {
            constructor = providerType.GetConstructor(Type.EmptyTypes);
        }

        // If still no constructor is found, throw an exception
        if (constructor == null)
        {
            throw new InvalidOperationException($"No suitable constructor found for {providerType.FullName}.");
        }

        Logger.Information($"Instantiating provider of type {providerType.Name}.");

        // Invoke the appropriate constructor
        return constructor.GetParameters().Length > 0
            ? constructor.Invoke([config])
            : constructor.Invoke(null);
    }
}
