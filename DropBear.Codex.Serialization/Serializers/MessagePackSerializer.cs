#region

using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger<DropBear.Codex.Serialization.Serializers.MessagePackSerializer>;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Serializer implementation for MessagePack serialization and deserialization.
/// </summary>
public sealed partial class MessagePackSerializer : ISerializer
{
    private readonly bool _enableCaching;
    private readonly ILogger _logger;
    private readonly int _maxCacheSize;
    private readonly RecyclableMemoryStreamManager _memoryManager;
    private readonly MessagePackSerializerOptions _options;

    // Cache for frequently serialized objects
    private readonly Dictionary<int, byte[]>? _serializationCache;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MessagePackSerializer" /> class.
    /// </summary>
    /// <param name="config">The serialization configuration.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if config or logger is null.</exception>
    public MessagePackSerializer(SerializationConfig config, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _options = config.MessagePackSerializerOptions ?? MessagePackSerializerOptions.Standard;
        _memoryManager = config.RecyclableMemoryStreamManager ?? throw new ArgumentNullException(
            nameof(config), "RecyclableMemoryStreamManager must be provided.");

        _enableCaching = config.EnableCaching;
        _maxCacheSize = config.MaxCacheSize;

        if (_enableCaching)
        {
            _serializationCache = new Dictionary<int, byte[]>(_maxCacheSize);
        }

        LogMessagePackSerializerInitialized(_logger, _options.Compression.ToString(), _options.Resolver?.GetType().Name ?? "null", _enableCaching);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> GetCapabilities()
    {
        return new Dictionary<string, object>
(StringComparer.Ordinal)
        {
            ["SerializerType"] = "MessagePack",
            ["Compression"] = _options.Compression.ToString(),
            ["Resolver"] = _options.Resolver?.GetType().Name ?? "Default",
            ["Security"] = _options.Security.ToString() ?? "Unknown",
            ["CacheEnabled"] = _enableCaching,
            ["MaxCacheSize"] = _maxCacheSize,
            ["IsThreadSafe"] = true
        };
    }

    /// <inheritdoc />
    public async Task<Result<byte[], SerializationError>> SerializeAsync<T>(T value,
        CancellationToken cancellationToken = default)
    {
        if (value == null)
        {
            LogSerializeNullValue(_logger, typeof(T).Name);
            return Result<byte[], SerializationError>.Success([]);
        }

        // Try cache lookup if enabled
        if (_enableCaching && _serializationCache != null)
        {
            var cacheKey = CalculateCacheKey(value);
            if (_serializationCache.TryGetValue(cacheKey, out var cachedBytes))
            {
                LogCacheHit(_logger, typeof(T).Name);
                return Result<byte[], SerializationError>.Success(cachedBytes);
            }
        }

        LogSerializationStarting(_logger, typeof(T).Name);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var memoryStream = _memoryManager.GetStream("MessagePackSerializer-Serialize");

            try
            {
                await MessagePack.MessagePackSerializer
                    .SerializeAsync(memoryStream, value, _options, cancellationToken)
                    .ConfigureAwait(false);

                var resultBytes = memoryStream.ToArray();

                stopwatch.Stop();
                LogSerializationCompleted(_logger, typeof(T).Name, stopwatch.ElapsedMilliseconds, resultBytes.Length);

                // Cache the result if enabled
                if (_enableCaching && _serializationCache != null)
                {
                    CacheSerializedResult(value, resultBytes);
                }

                return Result<byte[], SerializationError>.Success(resultBytes);
            }
            catch (MessagePackSerializationException ex) when (ex.InnerException is FormatterNotRegisteredException)
            {
                stopwatch.Stop();
                LogSerializationFormatterError(_logger, typeof(T).Name, ex);
                return Result<byte[], SerializationError>.Failure(
                    SerializationError.ForType<T>("Formatter not registered for type. Ensure all types are registered.",
                        "Serialize"),
                    ex);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogSerializationError(_logger, typeof(T).Name, ex.Message, ex);
            return Result<byte[], SerializationError>.Failure(
                SerializationError.ForType<T>($"MessagePack serialization failed: {ex.Message}", "Serialize"),
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<T, SerializationError>> DeserializeAsync<T>(byte[] data,
        CancellationToken cancellationToken = default)
    {
        if (data == null || data.Length == 0)
        {
            return Result<T, SerializationError>.Failure(
                SerializationError.ForType<T>("Cannot deserialize null or empty data", "Deserialize"));
        }

        LogDeserializationStarting(_logger, typeof(T).Name);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var memoryStream = _memoryManager.GetStream("MessagePackSerializer-Deserialize");

            try
            {
                await memoryStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                memoryStream.Seek(0, SeekOrigin.Begin);

                var result = await MessagePack.MessagePackSerializer
                    .DeserializeAsync<T>(memoryStream, _options, cancellationToken)
                    .ConfigureAwait(false);

                stopwatch.Stop();
                LogDeserializationCompleted(_logger, typeof(T).Name, stopwatch.ElapsedMilliseconds);

                return Result<T, SerializationError>.Success(result);
            }
            catch (MessagePackSerializationException ex)
            {
                stopwatch.Stop();
                LogDeserializationMessagePackError(_logger, typeof(T).Name, ex.Message, ex);
                return Result<T, SerializationError>.Failure(
                    SerializationError.ForType<T>($"MessagePack deserialization failed: {ex.Message}", "Deserialize"),
                    ex);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogDeserializationError(_logger, typeof(T).Name, ex.Message, ex);
            return Result<T, SerializationError>.Failure(
                SerializationError.ForType<T>($"MessagePack deserialization error: {ex.Message}", "Deserialize"),
                ex);
        }
    }

    #region Private Helper Methods

    /// <summary>
    ///     Calculates a cache key for the given value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to calculate a cache key for.</param>
    /// <returns>A cache key.</returns>
    private int CalculateCacheKey<T>(T value)
    {
        // Simple implementation - for more complex scenarios, consider a better hashing strategy
        return HashCode.Combine(typeof(T).GetHashCode(), value?.GetHashCode() ?? 0);
    }

    /// <summary>
    ///     Caches the serialized result for the given value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value that was serialized.</param>
    /// <param name="serializedData">The serialized data to cache.</param>
    private void CacheSerializedResult<T>(T value, byte[] serializedData)
    {
        if (!_enableCaching || _serializationCache == null)
        {
            return;
        }

        try
        {
            if (_serializationCache.Count >= _maxCacheSize)
            {
                // If the cache is full, remove the first entry (FIFO)
                var keyToRemove = _serializationCache.Keys.First();
                _serializationCache.Remove(keyToRemove);
            }

            var cacheKey = CalculateCacheKey(value);
            _serializationCache[cacheKey] = serializedData;
        }
        catch (Exception ex)
        {
            // Don't let caching errors affect the serialization flow
            LogCachingError(_logger, ex.Message, ex);
        }
    }

    #endregion

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Information, Message = "MessagePackSerializer initialized with Compression: {Compression}, Resolver: {Resolver}, EnableCaching: {EnableCaching}")]
    static partial void LogMessagePackSerializerInitialized(ILogger logger, string compression, string resolver, bool enableCaching);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Attempted to serialize null value of type {Type}")]
    static partial void LogSerializeNullValue(ILogger logger, string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cache hit for type {Type}, returning cached serialized data")]
    static partial void LogCacheHit(ILogger logger, string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting MessagePack serialization for type {Type}")]
    static partial void LogSerializationStarting(ILogger logger, string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "MessagePack serialization completed successfully for type {Type} in {ElapsedMs}ms. Result size: {ResultSize} bytes")]
    static partial void LogSerializationCompleted(ILogger logger, string type, long elapsedMs, int resultSize);

    [LoggerMessage(Level = LogLevel.Error, Message = "Serialization error: Formatter not registered for type {Type}")]
    static partial void LogSerializationFormatterError(ILogger logger, string type, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "General serialization error occurred for type {Type}: {Message}")]
    static partial void LogSerializationError(ILogger logger, string type, string message, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting MessagePack deserialization for type {Type}")]
    static partial void LogDeserializationStarting(ILogger logger, string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "MessagePack deserialization completed successfully for type {Type} in {ElapsedMs}ms")]
    static partial void LogDeserializationCompleted(ILogger logger, string type, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "MessagePack deserialization error for type {Type}: {Message}")]
    static partial void LogDeserializationMessagePackError(ILogger logger, string type, string message, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "General deserialization error occurred for type {Type}: {Message}")]
    static partial void LogDeserializationError(ILogger logger, string type, string message, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error caching serialization result: {Message}")]
    static partial void LogCachingError(ILogger logger, string message, Exception ex);

    #endregion
}
