#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using DropBear.Codex.Serialization.Serializers;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Factories;

/// <summary>
///     Factory for creating serializers based on configuration.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SerializerFactory
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForStaticClass(typeof(SerializerFactory));

    /// <summary>
    ///     Creates a serializer based on the provided configuration.
    /// </summary>
    /// <param name="config">The serialization configuration.</param>
    /// <returns>A configured serializer.</returns>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    public static ISerializer CreateSerializer(SerializationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));

        Logger.Information("Starting serializer creation with config: {Config}",
            new
            {
                SerializerType = config.SerializerType?.Name,
                HasCompression = config.CompressionProviderType != null
            });

        ValidateConfiguration(config);

        try
        {
            var serializer = CreateBaseSerializer(config);
            Logger.Information("Created base serializer of type {SerializerType}", serializer.GetType().Name);

            // Apply decorators in a specific order for optimal performance:
            // 1. First compress (reduces data size for subsequent operations)
            serializer = ApplyCompression(config, serializer);

            // 2. Then encrypt (operates on compressed data)
            serializer = ApplyEncryption(config, serializer);

            // 3. Finally encode (transforms binary data to text if needed)
            serializer = ApplyEncoding(config, serializer);

            Logger.Information("Serializer creation completed successfully.");
            return serializer;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to create serializer: {Message}", ex.Message);
            throw new InvalidOperationException($"Error creating serializer: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Creates a serializer based on the provided configuration with Result error handling.
    /// </summary>
    /// <param name="config">The serialization configuration.</param>
    /// <returns>A Result containing the configured serializer or an error.</returns>
    public static Result<ISerializer, SerializationError> CreateSerializerWithResult(SerializationConfig config)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(config, nameof(config));

            Logger.Information("Starting serializer creation with result handling.");

            var validationResult = ValidateConfigurationWithResult(config);
            if (!validationResult.IsSuccess)
            {
                return Result<ISerializer, SerializationError>.Failure(validationResult.Error!);
            }

            var serializer = CreateBaseSerializer(config);
            Logger.Information("Created base serializer of type {SerializerType}", serializer.GetType().Name);

            // Apply decorators in a specific order for optimal performance
            serializer = ApplyCompression(config, serializer);
            serializer = ApplyEncryption(config, serializer);
            serializer = ApplyEncoding(config, serializer);

            Logger.Information("Serializer creation completed successfully.");
            return Result<ISerializer, SerializationError>.Success(serializer);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to create serializer: {Message}", ex.Message);
            return Result<ISerializer, SerializationError>.Failure(
                new SerializationError($"Error creating serializer: {ex.Message}"), ex);
        }
    }

    private static void ValidateConfiguration(SerializationConfig config)
    {
        if (config.SerializerType == null)
        {
            var message = "Serializer type must be specified.";
            Logger.Error(message);
            throw new ArgumentException(message, nameof(config.SerializerType));
        }

        if (config.RecyclableMemoryStreamManager is null)
        {
            var message = "RecyclableMemoryStreamManager must be specified.";
            Logger.Error(message);
            throw new ArgumentException(message, nameof(config.RecyclableMemoryStreamManager));
        }

        Logger.Information("Configuration validated successfully.");
    }

    private static Result<Unit, SerializationError> ValidateConfigurationWithResult(SerializationConfig config)
    {
        if (config.SerializerType == null)
        {
            var message = "Serializer type must be specified.";
            Logger.Error(message);
            return Result<Unit, SerializationError>.Failure(new SerializationError(message));
        }

        if (config.RecyclableMemoryStreamManager is null)
        {
            var message = "RecyclableMemoryStreamManager must be specified.";
            Logger.Error(message);
            return Result<Unit, SerializationError>.Failure(new SerializationError(message));
        }

        return Result<Unit, SerializationError>.Success(Unit.Value);
    }

    private static ISerializer CreateBaseSerializer(SerializationConfig config)
    {
        var serializerType = config.SerializerType ?? throw new InvalidOperationException("Serializer type not set.");
        Logger.Information("Creating base serializer of type {SerializerType}", serializerType.Name);

        return InstantiateSerializer(config, serializerType);
    }

    private static ISerializer InstantiateSerializer(SerializationConfig config, Type serializerType)
    {
        try
        {
            var constructor = serializerType.GetConstructor([typeof(SerializationConfig)]);

            if (constructor == null)
            {
                throw new InvalidOperationException(
                    $"No suitable constructor found for {serializerType.FullName}. Ensure it has a constructor accepting SerializationConfig.");
            }

            Logger.Information("Instantiating serializer of type {SerializerType}", serializerType.Name);
            return (ISerializer)constructor.Invoke([config]);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to instantiate serializer of type {SerializerType}", serializerType.Name);
            throw new InvalidOperationException($"Error instantiating serializer: {ex.Message}", ex);
        }
    }

    private static ISerializer ApplyCompression(SerializationConfig config, ISerializer serializer)
    {
        if (config.CompressionProviderType == null)
        {
            Logger.Information("No compression provider configured.");
            return serializer;
        }

        try
        {
            var compressor = (ICompressionProvider)CreateProvider(config, config.CompressionProviderType);
            Logger.Information("Applying compression provider of type {ProviderType}",
                config.CompressionProviderType.Name);
            return new CompressedSerializer(serializer, compressor);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply compression provider of type {ProviderType}",
                config.CompressionProviderType.Name);
            throw new InvalidOperationException($"Error applying compression provider: {ex.Message}", ex);
        }
    }

    private static ISerializer ApplyEncryption(SerializationConfig config, ISerializer serializer)
    {
        if (config.EncryptionProviderType == null)
        {
            Logger.Information("No encryption provider configured.");
            return serializer;
        }

        try
        {
            var encryptor = (IEncryptionProvider)CreateProvider(config, config.EncryptionProviderType);
            Logger.Information("Applying encryption provider of type {ProviderType}",
                config.EncryptionProviderType.Name);
            return new EncryptedSerializer(serializer, encryptor);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply encryption provider of type {ProviderType}",
                config.EncryptionProviderType.Name);
            throw new InvalidOperationException($"Error applying encryption provider: {ex.Message}", ex);
        }
    }

    private static ISerializer ApplyEncoding(SerializationConfig config, ISerializer serializer)
    {
        if (config.EncodingProviderType == null)
        {
            Logger.Information("No encoding provider configured.");
            return serializer;
        }

        try
        {
            var encoder = (IEncodingProvider)CreateProvider(config, config.EncodingProviderType);
            Logger.Information("Applying encoding provider of type {ProviderType}", config.EncodingProviderType.Name);
            return new EncodedSerializer(serializer, encoder);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply encoding provider of type {ProviderType}",
                config.EncodingProviderType.Name);
            throw new InvalidOperationException($"Error applying encoding provider: {ex.Message}", ex);
        }
    }

    private static object CreateProvider(SerializationConfig config, Type providerType)
    {
        try
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
                throw new InvalidOperationException(
                    $"No suitable constructor found for {providerType.FullName}. " +
                    "Ensure it has either a parameterless constructor or one accepting SerializationConfig.");
            }

            Logger.Information("Instantiating provider of type {ProviderType}", providerType.Name);

            // Invoke the appropriate constructor
            return constructor.GetParameters().Length > 0
                ? constructor.Invoke([config])
                : constructor.Invoke(null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to create provider of type {ProviderType}", providerType.Name);
            throw new InvalidOperationException($"Error creating provider: {ex.Message}", ex);
        }
    }
}
