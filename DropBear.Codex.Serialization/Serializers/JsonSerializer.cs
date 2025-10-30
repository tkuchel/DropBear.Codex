#region

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Serializer implementation using System.Text.Json for JSON serialization and deserialization.
/// </summary>
public sealed class JsonSerializer : ISerializer
{
    private readonly int _bufferSize;
    private readonly bool _enableCaching;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<JsonSerializer>();
    private readonly int _maxCacheSize;
    private readonly RecyclableMemoryStreamManager _memoryManager;

    // Cache for frequently serialized objects
    private readonly Dictionary<int, byte[]> _serializationCache;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonSerializer" /> class.
    /// </summary>
    /// <param name="config">The serialization configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
    /// <exception cref="ArgumentException">Thrown when required configuration properties are missing.</exception>
    public JsonSerializer(SerializationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));

        Options = config.JsonSerializerOptions ?? new JsonSerializerOptions();
        _memoryManager = config.RecyclableMemoryStreamManager ?? throw new ArgumentNullException(
            nameof(config.RecyclableMemoryStreamManager), "RecyclableMemoryStreamManager must be provided.");

        _bufferSize = config.BufferSize;
        _enableCaching = config.EnableCaching;
        _maxCacheSize = config.MaxCacheSize;

        if (_enableCaching)
        {
            _serializationCache = new Dictionary<int, byte[]>(_maxCacheSize);
            _logger.Information("Caching enabled with max size: {MaxCacheSize}", _maxCacheSize);
        }

        _logger.Information("JsonSerializer initialized with WriteIndented: {WriteIndented}, " +
                            "PropertyNamingPolicy: {PropertyNamingPolicy}, BufferSize: {BufferSize}",
            Options.WriteIndented, Options.PropertyNamingPolicy?.GetType().Name ?? "null", _bufferSize);
    }

    /// <summary>
    ///     Gets the JSON serializer options being used.
    /// </summary>
    public JsonSerializerOptions Options { get; }

    /// <inheritdoc />
    public Dictionary<string, object> GetCapabilities()
    {
        return new Dictionary<string, object>
(StringComparer.Ordinal)
        {
            ["SerializerType"] = "JSON",
            ["WriteIndented"] = Options.WriteIndented,
            ["MaxDepth"] = Options.MaxDepth,
            ["PropertyNamingPolicy"] = Options.PropertyNamingPolicy?.GetType().Name ?? "null",
            ["CacheEnabled"] = _enableCaching,
            ["MaxCacheSize"] = _maxCacheSize,
            ["BufferSize"] = _bufferSize,
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
            return Result<byte[], SerializationError>.Success([]);
        }

        // For simple value types, use an optimized path
        if (TrySerializeSimpleType(value, out var simpleResult))
        {
            return Result<byte[], SerializationError>.Success(simpleResult);
        }

        // Try cache lookup if enabled
        if (_enableCaching)
        {
            var cacheKey = CalculateCacheKey(value);
            if (_serializationCache.TryGetValue(cacheKey, out var cachedBytes))
            {
                _logger.Information("Cache hit for type {Type}, returning cached serialized data.", typeof(T).Name);
                return Result<byte[], SerializationError>.Success(cachedBytes);
            }
        }

        _logger.Information("Starting JSON serialization for type {Type}.", typeof(T).Name);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var memoryStream = _memoryManager.GetStream("JsonSerializer-Serialize");

            try
            {
                await System.Text.Json.JsonSerializer.SerializeAsync(memoryStream, value, Options, cancellationToken)
                    .ConfigureAwait(false);

                var resultBytes = memoryStream.ToArray();
                stopwatch.Stop();

                _logger.Information("JSON serialization completed successfully for type {Type} in {ElapsedMs}ms. " +
                                    "Result size: {ResultSize} bytes",
                    typeof(T).Name, stopwatch.ElapsedMilliseconds, resultBytes.Length);

                // Cache the result if enabled
                if (_enableCaching && resultBytes.Length > 0)
                {
                    CacheSerializedResult(value, resultBytes);
                }

                return Result<byte[], SerializationError>.Success(resultBytes);
            }
            catch (JsonException ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "JSON serialization error for type {Type}: {Message}", typeof(T).Name, ex.Message);
                return Result<byte[], SerializationError>.Failure(
                    SerializationError.ForType<T>($"JSON serialization failed: {ex.Message}", "Serialize"), ex);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "Error during JSON serialization for type {Type}: {Message}", typeof(T).Name,
                    ex.Message);
                return Result<byte[], SerializationError>.Failure(
                    SerializationError.ForType<T>($"Error during serialization: {ex.Message}", "Serialize"), ex);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Stream creation error during JSON serialization for type {Type}: {Message}",
                typeof(T).Name, ex.Message);
            return Result<byte[], SerializationError>.Failure(
                SerializationError.ForType<T>($"Stream creation error: {ex.Message}", "Serialize"), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<T, SerializationError>> DeserializeAsync<T>(byte[] data,
        CancellationToken cancellationToken = default)
    {
        if (data == null || data.Length == 0)
        {
            _logger.Warning("Attempted to deserialize null or empty data to type {Type}", typeof(T).Name);
            return Result<T, SerializationError>.Failure(
                SerializationError.ForType<T>("Cannot deserialize null or empty data", "Deserialize"));
        }

        // For simple value types, use an optimized path
        if (TryDeserializeSimpleType<T>(data, out var simpleResult))
        {
            return Result<T, SerializationError>.Success(simpleResult);
        }

        _logger.Information("Starting JSON deserialization for type {Type}. Data size: {DataSize} bytes",
            typeof(T).Name, data.Length);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var memoryStream = _memoryManager.GetStream("JsonSerializer-Deserialize", data);
            memoryStream.Position = 0;

            try
            {
                var result = await System.Text.Json.JsonSerializer
                    .DeserializeAsync<T>(memoryStream, Options, cancellationToken)
                    .ConfigureAwait(false);

                stopwatch.Stop();

                if (result == null)
                {
                    _logger.Warning("JSON deserialization resulted in null for type {Type}", typeof(T).Name);
                    return Result<T, SerializationError>.Failure(
                        SerializationError.ForType<T>("Deserialization resulted in null", "Deserialize"));
                }

                _logger.Information("JSON deserialization completed successfully for type {Type} in {ElapsedMs}ms",
                    typeof(T).Name, stopwatch.ElapsedMilliseconds);

                return Result<T, SerializationError>.Success(result);
            }
            catch (JsonException ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "JSON deserialization error for type {Type}: {Message}", typeof(T).Name, ex.Message);
                return Result<T, SerializationError>.Failure(
                    SerializationError.ForType<T>($"JSON deserialization failed: {ex.Message}", "Deserialize"), ex);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "Error during JSON deserialization for type {Type}: {Message}", typeof(T).Name,
                    ex.Message);
                return Result<T, SerializationError>.Failure(
                    SerializationError.ForType<T>($"Error during deserialization: {ex.Message}", "Deserialize"), ex);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Stream creation error during JSON deserialization for type {Type}: {Message}",
                typeof(T).Name, ex.Message);
            return Result<T, SerializationError>.Failure(
                SerializationError.ForType<T>($"Stream creation error: {ex.Message}", "Deserialize"), ex);
        }
    }

    #region Private Helper Methods

    /// <summary>
    ///     Attempts to directly serialize simple types without using JsonSerializer.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="result">The resulting serialized bytes, if successful.</param>
    /// <returns>True if serialization was successful; otherwise, false.</returns>
    private bool TrySerializeSimpleType<T>(T value, out byte[] result)
    {
        result = [];
        var type = typeof(T);

        // Handle strings specially
        if (type == typeof(string) && value is string strValue)
        {
            // For strings, we can just UTF-8 encode the JSON string (with quotes)
            var jsonString = $"\"{strValue.Replace("\"", "\\\"")}\"";
            result = Encoding.UTF8.GetBytes(jsonString);
            return true;
        }

        // For primitive types, use UTF-8 encoding of their string representation
        if (type.IsPrimitive || type == typeof(decimal) || type == typeof(DateTime) ||
            type == typeof(TimeSpan) || type == typeof(Guid))
        {
            var jsonString = System.Text.Json.JsonSerializer.Serialize(value, Options);
            result = Encoding.UTF8.GetBytes(jsonString);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Attempts to directly deserialize simple types without using JsonSerializer.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="data">The serialized data.</param>
    /// <param name="result">The resulting deserialized object, if successful.</param>
    /// <returns>True if deserialization was successful; otherwise, false.</returns>
    private bool TryDeserializeSimpleType<T>(byte[] data, out T result)
    {
        result = default!;
        var type = typeof(T);

        // For primitive types, try to parse directly
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(Guid))
        {
            try
            {
                // Convert bytes to string and let JsonSerializer handle the parsing
                var jsonString = Encoding.UTF8.GetString(data);
                result = System.Text.Json.JsonSerializer.Deserialize<T>(jsonString, Options)!;
                return true;
            }
            catch
            {
                // If parsing fails, fallback to regular deserialization
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Calculates a cache key for the given value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to calculate a cache key for.</param>
    /// <returns>A cache key.</returns>
    private int CalculateCacheKey<T>(T value)
    {
        // For simple types, use their hash code
        if (value is int or long or float or double or decimal or bool or string or DateTime or TimeSpan or Guid)
        {
            return value?.GetHashCode() ?? 0;
        }

        // For complex types, combine the type hash with the object hash
        var typeHash = typeof(T).GetHashCode();
        var objectHash = value?.GetHashCode() ?? 0;

        return HashCode.Combine(typeHash, objectHash);
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
