#region

using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using MessagePack;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Serializer implementation for MessagePack serialization and deserialization.
/// </summary>
public sealed class MessagePackSerializer : ISerializer
{
    private readonly bool _enableCaching;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<MessagePackSerializer>();
    private readonly int _maxCacheSize;
    private readonly RecyclableMemoryStreamManager _memoryManager;
    private readonly MessagePackSerializerOptions _options;

    // Cache for frequently serialized objects
    private readonly Dictionary<int, byte[]> _serializationCache;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MessagePackSerializer" /> class.
    /// </summary>
    /// <param name="config">The serialization configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown if config is null.</exception>
    public MessagePackSerializer(SerializationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));

        _options = config.MessagePackSerializerOptions ?? MessagePackSerializerOptions.Standard;
        _memoryManager = config.RecyclableMemoryStreamManager ?? throw new ArgumentNullException(
            nameof(config.RecyclableMemoryStreamManager), "RecyclableMemoryStreamManager must be provided.");

        _enableCaching = config.EnableCaching;
        _maxCacheSize = config.MaxCacheSize;

        if (_enableCaching)
        {
            _serializationCache = new Dictionary<int, byte[]>(_maxCacheSize);
        }

        _logger.Information(
            "MessagePackSerializer initialized with Compression: {Compression}, Resolver: {Resolver}, " +
            "EnableCaching: {EnableCaching}",
            _options.Compression, _options.Resolver?.GetType().Name, _enableCaching);
    }

    /// <inheritdoc />
    public Dictionary<string, object> GetCapabilities()
    {
        return new Dictionary<string, object>
        {
            ["SerializerType"] = "MessagePack",
            ["Compression"] = _options.Compression.ToString(),
            ["Resolver"] = _options.Resolver?.GetType().Name ?? "Default",
            ["Security"] = _options.Security.ToString(),
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
            _logger.Warning("Attempted to serialize null value of type {Type}", typeof(T).Name);
            return Result<byte[], SerializationError>.Success(Array.Empty<byte>());
        }

        // Try cache lookup if enabled
        if (_enableCaching && _serializationCache != null)
        {
            var cacheKey = CalculateCacheKey(value);
            if (_serializationCache.TryGetValue(cacheKey, out var cachedBytes))
            {
                _logger.Information("Cache hit for type {Type}, returning cached serialized data.", typeof(T).Name);
                return Result<byte[], SerializationError>.Success(cachedBytes);
            }
        }

        _logger.Information("Starting MessagePack serialization for type {Type}.", typeof(T));
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
                _logger.Information(
                    "MessagePack serialization completed successfully for type {Type} in {ElapsedMs}ms. " +
                    "Result size: {ResultSize} bytes",
                    typeof(T).Name, stopwatch.ElapsedMilliseconds, resultBytes.Length);

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
                _logger.Error(ex, "Serialization error: Formatter not registered for type {Type}.", typeof(T));
                return Result<byte[], SerializationError>.Failure(
                    SerializationError.ForType<T>("Formatter not registered for type. Ensure all types are registered.",
                        "Serialize"),
                    ex);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "General serialization error occurred for type {Type}: {Message}", typeof(T), ex.Message);
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

        _logger.Information("Starting MessagePack deserialization for type {Type}.", typeof(T));
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
                _logger.Information(
                    "MessagePack deserialization completed successfully for type {Type} in {ElapsedMs}ms.",
                    typeof(T).Name, stopwatch.ElapsedMilliseconds);

                return Result<T, SerializationError>.Success(result);
            }
            catch (MessagePackSerializationException ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "MessagePack deserialization error for type {Type}: {Message}", typeof(T),
                    ex.Message);
                return Result<T, SerializationError>.Failure(
                    SerializationError.ForType<T>($"MessagePack deserialization failed: {ex.Message}", "Deserialize"),
                    ex);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "General deserialization error occurred for type {Type}: {Message}", typeof(T),
                ex.Message);
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
            _logger.Warning(ex, "Error caching serialization result: {Message}", ex.Message);
        }
    }

    #endregion
}
