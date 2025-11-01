#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using DropBear.Codex.Serialization.Serializers;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Serilog.ILogger;

#endregion

namespace DropBear.Codex.Serialization.Factories;

/// <summary>
///     Factory for creating serializers based on configuration.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SerializerFactory
{
    private static readonly ILogger Logger = DropBear.Codex.Core.Logging.LoggerFactory.Logger.ForStaticClass(typeof(SerializerFactory));

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
            // First, try to find a constructor with ILogger<T> parameter (new pattern for migrated serializers)
            var loggerType = typeof(Microsoft.Extensions.Logging.ILogger<>).MakeGenericType(serializerType);
            var constructorWithLogger = serializerType.GetConstructor([typeof(SerializationConfig), loggerType]);

            if (constructorWithLogger != null)
            {
                // Create MEL logger using LoggerFactory
                var loggerFactoryType = typeof(Microsoft.Extensions.Logging.LoggerFactory);
                var createMethod = loggerFactoryType.GetMethod(nameof(Microsoft.Extensions.Logging.LoggerFactory.Create),
                    [typeof(Action<Microsoft.Extensions.Logging.ILoggingBuilder>)]);

                if (createMethod == null)
                {
                    throw new InvalidOperationException("Could not find LoggerFactory.Create method");
                }

                // Create a logger factory that uses Serilog as the provider
                var loggerFactory = (Microsoft.Extensions.Logging.ILoggerFactory)createMethod.Invoke(null,
                    [new Action<Microsoft.Extensions.Logging.ILoggingBuilder>(builder =>
                    {
                        // Use Serilog logger
                        builder.AddSerilog(DropBear.Codex.Core.Logging.LoggerFactory.Logger.ForContext(serializerType));
                        builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    })])!;

                var melLogger = loggerFactory.CreateLogger(serializerType);

                Logger.Information("Instantiating serializer of type {SerializerType} with MEL logger (migrated pattern)",
                    serializerType.Name);
                return (ISerializer)constructorWithLogger.Invoke([config, melLogger]);
            }

            // Fallback to old constructor pattern (SerializationConfig only)
            var constructor = serializerType.GetConstructor([typeof(SerializationConfig)]);

            if (constructor == null)
            {
                throw new InvalidOperationException(
                    $"No suitable constructor found for {serializerType.FullName}. " +
                    "Ensure it has a constructor accepting either (SerializationConfig, ILogger<T>) or (SerializationConfig).");
            }

            Logger.Information("Instantiating serializer of type {SerializerType} (legacy pattern)", serializerType.Name);
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

            // Create MEL logger for CompressedSerializer
            var loggerFactoryType = typeof(Microsoft.Extensions.Logging.LoggerFactory);
            var createMethod = loggerFactoryType.GetMethod(nameof(Microsoft.Extensions.Logging.LoggerFactory.Create),
                [typeof(Action<Microsoft.Extensions.Logging.ILoggingBuilder>)]);

            if (createMethod == null)
            {
                throw new InvalidOperationException("Could not find LoggerFactory.Create method");
            }

            var loggerFactory = (Microsoft.Extensions.Logging.ILoggerFactory)createMethod.Invoke(null,
                [new Action<Microsoft.Extensions.Logging.ILoggingBuilder>(builder =>
                {
                    builder.AddSerilog(DropBear.Codex.Core.Logging.LoggerFactory.Logger.ForContext<CompressedSerializer>());
                    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                })])!;

            var melLogger = loggerFactory.CreateLogger<CompressedSerializer>();

            return new CompressedSerializer(serializer, compressor, melLogger);
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

            // Create MEL logger for EncryptedSerializer
            var loggerFactoryType = typeof(Microsoft.Extensions.Logging.LoggerFactory);
            var createMethod = loggerFactoryType.GetMethod(nameof(Microsoft.Extensions.Logging.LoggerFactory.Create),
                [typeof(Action<Microsoft.Extensions.Logging.ILoggingBuilder>)]);

            if (createMethod == null)
            {
                throw new InvalidOperationException("Could not find LoggerFactory.Create method");
            }

            var loggerFactory = (Microsoft.Extensions.Logging.ILoggerFactory)createMethod.Invoke(null,
                [new Action<Microsoft.Extensions.Logging.ILoggingBuilder>(builder =>
                {
                    builder.AddSerilog(DropBear.Codex.Core.Logging.LoggerFactory.Logger.ForContext<EncryptedSerializer>());
                    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                })])!;

            var melLogger = loggerFactory.CreateLogger<EncryptedSerializer>();

            return new EncryptedSerializer(serializer, encryptor, melLogger);
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

            // Create MEL logger for EncodedSerializer
            var loggerFactoryType = typeof(Microsoft.Extensions.Logging.LoggerFactory);
            var createMethod = loggerFactoryType.GetMethod(nameof(Microsoft.Extensions.Logging.LoggerFactory.Create),
                [typeof(Action<Microsoft.Extensions.Logging.ILoggingBuilder>)]);

            if (createMethod == null)
            {
                throw new InvalidOperationException("Could not find LoggerFactory.Create method");
            }

            var loggerFactory = (Microsoft.Extensions.Logging.ILoggerFactory)createMethod.Invoke(null,
                [new Action<Microsoft.Extensions.Logging.ILoggingBuilder>(builder =>
                {
                    builder.AddSerilog(DropBear.Codex.Core.Logging.LoggerFactory.Logger.ForContext<EncodedSerializer>());
                    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                })])!;

            var melLogger = loggerFactory.CreateLogger<EncodedSerializer>();

            return new EncodedSerializer(serializer, encoder, melLogger);
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
