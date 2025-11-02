#region

using System.Runtime.Versioning;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Converters;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using DropBear.Codex.Serialization.Providers;
using DropBear.Codex.Serialization.Serializers;
using MessagePack;
using MessagePack.ImmutableCollection;
using MessagePack.Resolvers;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using Serilog;
using JsonSerializer = DropBear.Codex.Serialization.Serializers.JsonSerializer;
using MessagePackSerializer = DropBear.Codex.Serialization.Serializers.MessagePackSerializer;
using ILogger = Microsoft.Extensions.Logging.ILogger<DropBear.Codex.Serialization.Factories.SerializationBuilder>;

#endregion

namespace DropBear.Codex.Serialization.Factories;

/// <summary>
///     Builder class for configuring serialization settings and creating serializers.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class SerializationBuilder
{
    private readonly SerializationConfig _config;
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SerializationBuilder" /> class.
    /// </summary>
    /// <param name="logger">The logger instance. If null, creates a default logger.</param>
    public SerializationBuilder(ILogger? logger = null)
    {
        _config = new SerializationConfig();

        // Create logger if not provided
        _logger = logger ?? CreateDefaultLogger();

        LogSerializationBuilderInitialized();
    }

    private static ILogger CreateDefaultLogger()
    {
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(DropBear.Codex.Core.Logging.LoggerFactory.Logger.ForContext<SerializationBuilder>());
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
        });
        return loggerFactory.CreateLogger<SerializationBuilder>();
    }

    /// <summary>
    ///     Configures the builder to use the specified serializer type.
    /// </summary>
    /// <typeparam name="T">The type of serializer to use.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithSerializer<T>() where T : ISerializer
    {
        LogConfiguringSerializerType(typeof(T).Name);
        _config.SerializerType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Configures the builder to use the specified serializer type.
    /// </summary>
    /// <param name="serializerType">The type of serializer to use.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when serializerType is null.</exception>
    /// <exception cref="ArgumentException">Thrown when serializerType does not implement ISerializer.</exception>
    public SerializationBuilder WithSerializer(Type serializerType)
    {
        ArgumentNullException.ThrowIfNull(serializerType, nameof(serializerType));

        if (!typeof(ISerializer).IsAssignableFrom(serializerType))
        {
            var message = $"The type {serializerType.Name} must implement the ISerializer interface.";
            LogSerializerTypeValidationError(message);
            throw new ArgumentException(message, nameof(serializerType));
        }

        LogConfiguringSerializerType(serializerType.Name);
        _config.SerializerType = serializerType;
        return this;
    }

    /// <summary>
    ///     Configures the builder to use the specified compression provider type.
    /// </summary>
    /// <param name="compressionType">The type of compression provider to use.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithCompression(Type compressionType)
    {
        ValidateProviderType<ICompressionProvider>(compressionType, nameof(compressionType));
        LogConfiguringCompressionProvider(compressionType.Name);
        _config.CompressionProviderType = compressionType;
        return this;
    }

    /// <summary>
    ///     Configures the builder to use the specified encoding provider type.
    /// </summary>
    /// <param name="encodingType">The type of encoding provider to use.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithEncoding(Type encodingType)
    {
        ValidateProviderType<IEncodingProvider>(encodingType, nameof(encodingType));
        LogConfiguringEncodingProvider(encodingType.Name);
        _config.EncodingProviderType = encodingType;
        return this;
    }

    /// <summary>
    ///     Configures the builder to use the specified encryption provider type.
    /// </summary>
    /// <param name="encryptionType">The type of encryption provider to use.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithEncryption(Type encryptionType)
    {
        ValidateProviderType<IEncryptionProvider>(encryptionType, nameof(encryptionType));
        LogConfiguringEncryptionProvider(encryptionType.Name);
        _config.EncryptionProviderType = encryptionType;
        return this;
    }

    /// <summary>
    ///     Configures the builder to use the specified encryption provider type.
    /// </summary>
    /// <typeparam name="T">The type of encryption provider to use.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithEncryption<T>() where T : IEncryptionProvider
    {
        LogConfiguringEncryptionProvider(typeof(T).Name);
        _config.EncryptionProviderType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Configures the builder to use the specified stream serializer type.
    /// </summary>
    /// <typeparam name="T">The type of stream serializer to use.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithStreamSerializer<T>() where T : IStreamSerializer
    {
        LogConfiguringStreamSerializer(typeof(T).Name);
        _config.StreamSerializerType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Configures the builder to use the specified streaming serializer type
    ///     for element-by-element JSON array deserialization.
    /// </summary>
    /// <typeparam name="T">The type of streaming serializer to use.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithStreamingSerializer<T>() where T : IStreamingSerializer
    {
        LogConfiguringStreamingSerializer(typeof(T).Name);
        _config.StreamingSerializerType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Configures the builder to use the specified compression provider type.
    /// </summary>
    /// <typeparam name="T">The type of compression provider to use.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithCompression<T>() where T : ICompressionProvider
    {
        LogConfiguringCompressionProvider(typeof(T).Name);
        _config.CompressionProviderType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Configures the builder to use the specified encoding provider type.
    /// </summary>
    /// <typeparam name="T">The type of encoding provider to use.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithEncoding<T>() where T : IEncodingProvider
    {
        LogConfiguringEncodingProvider(typeof(T).Name);
        _config.EncodingProviderType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Configures the builder with encryption key paths.
    /// </summary>
    /// <param name="publicKeyPath">The path to the public key file.</param>
    /// <param name="privateKeyPath">The path to the private key file.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithKeys(string publicKeyPath, string privateKeyPath)
    {
        ValidateFileExists(publicKeyPath, "Public key file");
        ValidateFileExists(privateKeyPath, "Private key file");

        LogConfiguringKeyPaths();
        _config.PublicKeyPath = publicKeyPath;
        _config.PrivateKeyPath = privateKeyPath;

        return this;
    }

    /// <summary>
    ///     Configures the builder with JSON serializer options.
    /// </summary>
    /// <param name="options">The JSON serializer options.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithJsonSerializerOptions(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        _config.SerializerType ??= typeof(JsonSerializer);
        _config.JsonSerializerOptions = options;
        LogJsonSerializerOptionsConfigured();
        return this;
    }

    /// <summary>
    ///     Configures the builder with a JSON serializer context for source-generated serialization.
    ///     Provides 20-30% performance improvement over reflection-based JSON serialization.
    /// </summary>
    /// <param name="context">The JSON serializer context for source generation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithJsonSerializerContext(JsonSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        _config.SerializerType ??= typeof(JsonSerializer);
        _config.JsonSerializerContext = context;
        LogJsonSerializerContextConfigured(context.GetType().Name);
        return this;
    }

    /// <summary>
    ///     Configures the builder with optimized default JSON serializer options.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithDefaultJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReferenceHandler = ReferenceHandler.Preserve,
            MaxDepth = 64,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(), new TimeSpanConverter() // Custom TimeSpan converter
            }
        };

        _config.SerializerType ??= typeof(JsonSerializer);
        _config.JsonSerializerOptions = options;
        LogDefaultJsonSerializerOptionsConfigured();
        return this;
    }

    /// <summary>
    ///     Configures the builder with MessagePack serializer options.
    /// </summary>
    /// <param name="options">The MessagePack serializer options.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithMessagePackSerializerOptions(MessagePackSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        _config.MessagePackSerializerOptions = options;
        LogMessagePackSerializerOptionsConfigured();
        return this;
    }

    /// <summary>
    ///     Configures the builder with optimized default MessagePack serializer options.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithDefaultMessagePackSerializerOptions()
    {
        var options = MessagePackSerializerOptions.Standard
            .WithResolver(CompositeResolver.Create(
                ImmutableCollectionResolver.Instance,
                StandardResolverAllowPrivate.Instance,
                StandardResolver.Instance
            ))
            .WithCompression(MessagePackCompression.Lz4BlockArray)
            .WithSecurity(MessagePackSecurity.UntrustedData);

        _config.MessagePackSerializerOptions = options;
        LogDefaultMessagePackSerializerOptionsConfigured();
        return this;
    }

    /// <summary>
    ///     Configures the builder with memory-related settings.
    /// </summary>
    /// <param name="bufferSize">The size of buffers to use during operations.</param>
    /// <param name="maxMemoryThreshold">The maximum memory threshold for operations.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithMemorySettings(int bufferSize = 81920, long maxMemoryThreshold = 104857600)
    {
        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be greater than zero.");
        }

        if (maxMemoryThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMemoryThreshold),
                "Memory threshold must be greater than zero.");
        }

        _config.BufferSize = bufferSize;
        _config.MaxMemoryThreshold = maxMemoryThreshold;

        // Configure a new RecyclableMemoryStreamManager with these settings
        _config.RecyclableMemoryStreamManager = new RecyclableMemoryStreamManager(
            new RecyclableMemoryStreamManager.Options
            {
                BlockSize = 4096, // 4KB blocks
                LargeBufferMultiple = 1024 * 1024, // 1MB buffer increments
                MaximumBufferSize = (int)maxMemoryThreshold
            });

        LogMemorySettingsConfigured(bufferSize, maxMemoryThreshold);
        return this;
    }

    /// <summary>
    ///     Configures the builder with caching settings.
    /// </summary>
    /// <param name="enableCaching">Whether to enable caching.</param>
    /// <param name="maxCacheSize">The maximum number of items to cache.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithCaching(bool enableCaching = true, int maxCacheSize = 100)
    {
        if (maxCacheSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCacheSize), "Cache size must be greater than zero.");
        }

        _config.EnableCaching = enableCaching;
        _config.MaxCacheSize = maxCacheSize;

        LogCachingConfigured(enableCaching, maxCacheSize);
        return this;
    }

    /// <summary>
    ///     Configures the builder with default serialization settings.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public SerializationBuilder WithDefaultConfiguration()
    {
        LogConfiguringDefaultSettings();
        return WithJsonSerializerOptions(new JsonSerializerOptions { WriteIndented = true })
            .WithCompression<GZipCompressionProvider>()
            .WithEncoding<Base64EncodingProvider>()
            .WithMemorySettings()
            .WithCaching();
    }

    /// <summary>
    ///     Builds and returns a serializer based on the configured settings.
    /// </summary>
    /// <returns>A Result containing the configured serializer or an error.</returns>
    public Result<ISerializer, SerializationError> Build()
    {
        LogBuildingSerializer();

        try
        {
            _config.SerializerType = _config.SerializerType switch
            {
                null when _config.StreamSerializerType is null =>
                    throw new InvalidOperationException(
                        "No serializer type or stream serializer specified. Please specify one before building."),
                null when _config.StreamSerializerType is not null => typeof(StreamSerializerAdapter),
                _ => _config.SerializerType
            };

            var validationResult = ValidateSerializerConfigurations();
            if (!validationResult.IsSuccess)
            {
                return Result<ISerializer, SerializationError>.Failure(validationResult.Error!);
            }

            // Create the serializer using the factory
            var serializer = SerializerFactory.CreateSerializer(_config);

            LogSerializerBuiltSuccessfully(_config.SerializerType?.Name ?? "Unknown");
            return Result<ISerializer, SerializationError>.Success(serializer);
        }
        catch (Exception ex)
        {
            LogErrorBuildingSerializer(ex, ex.Message);
            return Result<ISerializer, SerializationError>.Failure(
                new SerializationError($"Failed to build serializer: {ex.Message}"), ex);
        }
    }

    private void ValidateProviderType<T>(Type providerType, string paramName)
    {
        ArgumentNullException.ThrowIfNull(providerType, paramName);

        if (!typeof(T).IsAssignableFrom(providerType))
        {
            var message = $"The type {providerType.Name} must implement the {typeof(T).Name} interface.";
            LogProviderTypeValidationError(message);
            throw new ArgumentException(message, paramName);
        }
    }

    private static void ValidateFileExists(string path, string fileType)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException($"{fileType} path cannot be null or empty.", nameof(path));
        }

        if (!File.Exists(path))
        {
            var message = $"{fileType} not found at path: {path}";
            throw new FileNotFoundException(message, path);
        }
    }

    private Result<Unit, SerializationError> ValidateSerializerConfigurations()
    {
        try
        {
            // Check specific serializer requirements
            if (_config.SerializerType == typeof(JsonSerializer) && _config.JsonSerializerOptions is null)
            {
                const string message = "JsonSerializerOptions must be specified for JsonSerializer.";
                LogValidationErrorJsonSerializerOptions(message);
                return Result<Unit, SerializationError>.Failure(new SerializationError(message));
            }

            if (_config.SerializerType == typeof(MessagePackSerializer) && _config.MessagePackSerializerOptions is null)
            {
                const string message = "MessagePackSerializerOptions must be specified for MessagePackSerializer.";
                LogValidationErrorMessagePackSerializerOptions(message);
                return Result<Unit, SerializationError>.Failure(new SerializationError(message));
            }

            // Check encryption provider requirements
            if (_config.EncryptionProviderType != null &&
                (string.IsNullOrEmpty(_config.PublicKeyPath) || string.IsNullOrEmpty(_config.PrivateKeyPath)))
            {
                const string message = "PublicKeyPath and PrivateKeyPath must be specified when using encryption.";
                LogValidationErrorEncryptionKeyPaths(message);
                return Result<Unit, SerializationError>.Failure(new SerializationError(message));
            }

            // Validate memory settings
            if (_config.BufferSize <= 0)
            {
                const string message = "BufferSize must be greater than zero.";
                LogValidationErrorBufferSize(message);
                return Result<Unit, SerializationError>.Failure(new SerializationError(message));
            }

            if (_config.MaxMemoryThreshold <= 0)
            {
                const string message = "MaxMemoryThreshold must be greater than zero.";
                LogValidationErrorMaxMemoryThreshold(message);
                return Result<Unit, SerializationError>.Failure(new SerializationError(message));
            }

            return Result<Unit, SerializationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            LogValidationError(ex, ex.Message);
            return Result<Unit, SerializationError>.Failure(
                new SerializationError($"Configuration validation failed: {ex.Message}"), ex);
        }
    }

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Information, Message = "SerializationBuilder initialized")]
    private partial void LogSerializationBuilderInitialized();

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuring serializer type: {Type}")]
    private partial void LogConfiguringSerializerType(string type);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    private partial void LogSerializerTypeValidationError(string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuring compression provider: {Type}")]
    private partial void LogConfiguringCompressionProvider(string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuring encoding provider: {Type}")]
    private partial void LogConfiguringEncodingProvider(string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuring encryption provider: {Type}")]
    private partial void LogConfiguringEncryptionProvider(string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuring stream serializer: {Type}")]
    private partial void LogConfiguringStreamSerializer(string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuring streaming serializer: {Type}")]
    private partial void LogConfiguringStreamingSerializer(string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuring key paths for encryption.")]
    private partial void LogConfiguringKeyPaths();

    [LoggerMessage(Level = LogLevel.Information, Message = "Configured JSON serializer options.")]
    private partial void LogJsonSerializerOptionsConfigured();

    [LoggerMessage(Level = LogLevel.Information, Message = "Configured JSON serializer context for source-generated serialization: {ContextType}")]
    private partial void LogJsonSerializerContextConfigured(string contextType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configured default JSON serializer options.")]
    private partial void LogDefaultJsonSerializerOptionsConfigured();

    [LoggerMessage(Level = LogLevel.Information, Message = "Configured MessagePack serializer options.")]
    private partial void LogMessagePackSerializerOptionsConfigured();

    [LoggerMessage(Level = LogLevel.Information, Message = "Configured default MessagePack serializer options.")]
    private partial void LogDefaultMessagePackSerializerOptionsConfigured();

    [LoggerMessage(Level = LogLevel.Information, Message = "Configured memory settings. BufferSize: {BufferSize}, MaxMemoryThreshold: {MaxMemoryThreshold}")]
    private partial void LogMemorySettingsConfigured(int bufferSize, long maxMemoryThreshold);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configured caching. EnableCaching: {EnableCaching}, MaxCacheSize: {MaxCacheSize}")]
    private partial void LogCachingConfigured(bool enableCaching, int maxCacheSize);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuring default serialization settings.")]
    private partial void LogConfiguringDefaultSettings();

    [LoggerMessage(Level = LogLevel.Information, Message = "Building the serializer.")]
    private partial void LogBuildingSerializer();

    [LoggerMessage(Level = LogLevel.Information, Message = "Serializer built successfully with type: {SerializerType}")]
    private partial void LogSerializerBuiltSuccessfully(string serializerType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error building serializer: {Message}")]
    private partial void LogErrorBuildingSerializer(Exception ex, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    private partial void LogProviderTypeValidationError(string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    private partial void LogValidationErrorJsonSerializerOptions(string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    private partial void LogValidationErrorMessagePackSerializerOptions(string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    private partial void LogValidationErrorEncryptionKeyPaths(string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    private partial void LogValidationErrorBufferSize(string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    private partial void LogValidationErrorMaxMemoryThreshold(string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Validation error: {Message}")]
    private partial void LogValidationError(Exception ex, string message);

    #endregion
}
