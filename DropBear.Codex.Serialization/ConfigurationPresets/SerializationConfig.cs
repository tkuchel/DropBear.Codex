#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using MessagePack;
using Microsoft.IO;

#endregion

namespace DropBear.Codex.Serialization.ConfigurationPresets;

/// <summary>
///     Represents the configuration settings for serialization.
/// </summary>
public sealed class SerializationConfig
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SerializationConfig" /> class with default settings.
    /// </summary>
    public SerializationConfig()
    {
        // Initialize with optimized default values
        RecyclableMemoryStreamManager = new RecyclableMemoryStreamManager(new RecyclableMemoryStreamManager.Options
        {
            BlockSize = 4096, // 4KB blocks
            LargeBufferMultiple = 1024 * 1024, // 1MB buffer increments
            MaximumBufferSize = (int)MaxMemoryThreshold
        });
    }

    /// <summary>
    ///     Gets or sets the type of serializer to be used.
    /// </summary>
    public Type? SerializerType { get; set; }

    /// <summary>
    ///     Gets or sets the type of compression provider for serialization.
    /// </summary>
    public Type? CompressionProviderType { get; set; }

    /// <summary>
    ///     Gets or sets the type of encoding provider for serialization.
    /// </summary>
    public Type? EncodingProviderType { get; set; }

    /// <summary>
    ///     Gets or sets the type of encryption provider for serialization.
    /// </summary>
    public Type? EncryptionProviderType { get; set; }

    /// <summary>
    ///     Gets or sets the memory stream manager for serialization.
    /// </summary>
    public RecyclableMemoryStreamManager RecyclableMemoryStreamManager { get; set; }

    /// <summary>
    ///     Gets or sets the type of stream serializer for serialization.
    /// </summary>
    public Type? StreamSerializerType { get; set; }

    /// <summary>
    ///     Gets or sets the type of streaming serializer for element-by-element JSON array deserialization.
    /// </summary>
    public Type? StreamingSerializerType { get; set; }

    /// <summary>
    ///     Gets or sets the JSON serializer options.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }

    /// <summary>
    ///     Gets or sets the JSON serializer context for source-generated serialization.
    ///     When provided, enables 20-30% performance improvement over reflection-based serialization.
    /// </summary>
    public JsonSerializerContext? JsonSerializerContext { get; set; }

    /// <summary>
    ///     Gets or sets the MessagePack serializer options.
    /// </summary>
    public MessagePackSerializerOptions? MessagePackSerializerOptions { get; set; }

    /// <summary>
    ///     Gets or sets the path to the public key file.
    /// </summary>
    public string PublicKeyPath { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the path to the private key file.
    /// </summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the buffer size for stream operations.
    /// </summary>
    public int BufferSize { get; set; } = 81920; // Default 80KB buffer size

    /// <summary>
    ///     Gets or sets whether shared buffers should be used.
    /// </summary>
    public bool UseSharedBuffers { get; set; } = true;

    /// <summary>
    ///     Gets or sets the maximum memory threshold for operations.
    /// </summary>
    public long MaxMemoryThreshold { get; set; } = 1024 * 1024 * 100; // 100MB default threshold

    /// <summary>
    ///     Gets or sets whether caching should be used for serialization operations.
    /// </summary>
    public bool EnableCaching { get; set; }

    /// <summary>
    ///     Gets or sets the maximum size of the serialization cache.
    /// </summary>
    public int MaxCacheSize { get; set; } = 100;

    /// <summary>
    ///     Gets or sets the timeout for serialization operations in milliseconds.
    /// </summary>
    public int OperationTimeoutMs { get; set; } = 30000; // 30 seconds default

    /// <summary>
    ///     Validates the configuration to ensure required properties are set.
    /// </summary>
    /// <returns>A result indicating success or failure of the validation.</returns>
    public Result<Unit, SerializationError> Validate()
    {
        var errors = new List<string>();

        if (SerializerType == null)
        {
            errors.Add("SerializerType must be specified in the configuration.");
        }

        // Handle specific serializer requirements
        if (SerializerType != null)
        {
            if (SerializerType.Name.Contains("Json", StringComparison.OrdinalIgnoreCase) &&
                JsonSerializerOptions == null)
            {
                errors.Add("JsonSerializerOptions must be specified when using a JSON serializer.");
            }

            if (SerializerType.Name.Contains("MessagePack", StringComparison.OrdinalIgnoreCase) &&
                MessagePackSerializerOptions == null)
            {
                errors.Add("MessagePackSerializerOptions must be specified when using a MessagePack serializer.");
            }
        }

        // Check key paths if encryption is specified
        if (EncryptionProviderType != null)
        {
            if (string.IsNullOrEmpty(PublicKeyPath))
            {
                errors.Add("PublicKeyPath must be specified when using encryption.");
            }

            if (string.IsNullOrEmpty(PrivateKeyPath))
            {
                errors.Add("PrivateKeyPath must be specified when using encryption.");
            }
        }

        // Validate buffer size and memory thresholds
        if (BufferSize <= 0)
        {
            errors.Add("BufferSize must be greater than zero.");
        }

        if (MaxMemoryThreshold <= 0)
        {
            errors.Add("MaxMemoryThreshold must be greater than zero.");
        }

        if (errors.Count > 0)
        {
            return Result<Unit, SerializationError>.Failure(
                new SerializationError($"Configuration validation failed: {string.Join("; ", errors)}"));
        }

        return Result<Unit, SerializationError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Sets the serializer type and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <typeparam name="T">The type of serializer.</typeparam>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithSerializerType<T>()
    {
        SerializerType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Sets the compression provider type and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <typeparam name="T">The type of compression provider.</typeparam>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithCompressionProviderType<T>()
    {
        CompressionProviderType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Sets the encoding provider type and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <typeparam name="T">The type of encoding provider.</typeparam>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithEncodingProviderType<T>()
    {
        EncodingProviderType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Sets the encryption provider type and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <typeparam name="T">The type of encryption provider.</typeparam>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithEncryptionProviderType<T>()
    {
        EncryptionProviderType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Sets the stream serializer type and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <typeparam name="T">The type of stream serializer.</typeparam>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithStreamSerializerType<T>()
    {
        StreamSerializerType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Sets the streaming serializer type and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <typeparam name="T">The type of streaming serializer.</typeparam>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithStreamingSerializerType<T>()
    {
        StreamingSerializerType = typeof(T);
        return this;
    }

    /// <summary>
    ///     Sets the JSON serializer options and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <param name="options">The JSON serializer options.</param>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithJsonSerializerOptions(JsonSerializerOptions options)
    {
        JsonSerializerOptions = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>
    ///     Sets the JSON serializer context for source-generated serialization and returns the updated configuration for fluent chaining.
    ///     Provides 20-30% performance improvement over reflection-based JSON serialization.
    /// </summary>
    /// <param name="context">The JSON serializer context for source generation.</param>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithJsonSerializerContext(JsonSerializerContext context)
    {
        JsonSerializerContext = context ?? throw new ArgumentNullException(nameof(context));
        return this;
    }

    /// <summary>
    ///     Sets the MessagePack serializer options and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <param name="options">The MessagePack serializer options.</param>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithMessagePackSerializerOptions(MessagePackSerializerOptions options)
    {
        MessagePackSerializerOptions = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>
    ///     Sets the public key path and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <param name="path">The public key file path.</param>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithPublicKeyPath(string path)
    {
        PublicKeyPath = path ?? throw new ArgumentNullException(nameof(path));
        return this;
    }

    /// <summary>
    ///     Sets the private key path and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <param name="path">The private key file path.</param>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithPrivateKeyPath(string path)
    {
        PrivateKeyPath = path ?? throw new ArgumentNullException(nameof(path));
        return this;
    }

    /// <summary>
    ///     Sets the buffer size and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <param name="bufferSize">The buffer size in bytes.</param>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithBufferSize(int bufferSize)
    {
        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be greater than zero.");
        }

        BufferSize = bufferSize;
        return this;
    }

    /// <summary>
    ///     Enables or disables caching and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <param name="enableCaching">Whether to enable caching.</param>
    /// <param name="maxCacheSize">The maximum cache size.</param>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithCaching(bool enableCaching, int maxCacheSize = 100)
    {
        if (maxCacheSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCacheSize), "Max cache size must be greater than zero.");
        }

        EnableCaching = enableCaching;
        MaxCacheSize = maxCacheSize;
        return this;
    }

    /// <summary>
    ///     Sets the operation timeout and returns the updated configuration for fluent chaining.
    /// </summary>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The updated configuration.</returns>
    public SerializationConfig WithTimeout(int timeoutMs)
    {
        if (timeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Timeout must be greater than zero.");
        }

        OperationTimeoutMs = timeoutMs;
        return this;
    }

    /// <summary>
    ///     Creates a memory stream with the configured options.
    /// </summary>
    /// <param name="tag">An optional tag for the stream.</param>
    /// <returns>A recyclable memory stream.</returns>
    public RecyclableMemoryStream CreateMemoryStream(string? tag = null)
    {
        return RecyclableMemoryStreamManager.GetStream(tag ?? "SerializationConfig");
    }
}
