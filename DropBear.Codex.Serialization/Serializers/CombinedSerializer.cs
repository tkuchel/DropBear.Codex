#region

using System.Diagnostics;
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
///     A serializer that combines both regular and stream-based serialization strategies.
///     It chooses the appropriate strategy based on the context or type of the data.
/// </summary>
public sealed class CombinedSerializer : ISerializer
{
    private readonly SerializationConfig _config;
    private readonly ISerializer _defaultSerializer;
    private readonly int _largeSizeThreshold;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<CombinedSerializer>();
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;
    private readonly IStreamSerializer _streamSerializer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CombinedSerializer" /> class.
    /// </summary>
    /// <param name="config">The serialization configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown if config is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if required serializers cannot be created.</exception>
    public CombinedSerializer(SerializationConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _largeSizeThreshold =
            config.MaxMemoryThreshold > 0 ? (int)config.MaxMemoryThreshold : 1024 * 1024 * 10; // 10 MB default
        _memoryStreamManager = config.RecyclableMemoryStreamManager;

        try
        {
            _defaultSerializer = CreateProvider<ISerializer>(config.SerializerType)
                                 ?? throw new InvalidOperationException(
                                     "Default serializer must implement ISerializer.");

            _streamSerializer = CreateProvider<IStreamSerializer>(config.StreamSerializerType)
                                ?? throw new InvalidOperationException(
                                    "Stream serializer must implement IStreamSerializer.");

            _logger.Information("CombinedSerializer initialized with DefaultSerializer: {DefaultSerializer}, " +
                                "StreamSerializer: {StreamSerializer}, LargeSizeThreshold: {LargeSizeThreshold} bytes",
                _defaultSerializer.GetType().Name, _streamSerializer.GetType().Name, _largeSizeThreshold);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize CombinedSerializer: {Message}", ex.Message);
            throw new InvalidOperationException("Error initializing combined serializer.", ex);
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> GetCapabilities()
    {
        var defaultCapabilities = _defaultSerializer.GetCapabilities();

        var capabilities = new Dictionary<string, object>(defaultCapabilities, StringComparer.Ordinal)
        {
            ["SerializerType"] = "CombinedSerializer",
            ["DefaultSerializerType"] = _defaultSerializer.GetType().Name,
            ["StreamSerializerType"] = _streamSerializer.GetType().Name,
            ["LargeSizeThreshold"] = _largeSizeThreshold
        };

        return capabilities;
    }

    /// <inheritdoc />
    public async Task<Result<byte[], SerializationError>> SerializeAsync<T>(T value,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Special handling for Stream type
            if (value is Stream streamValue)
            {
                _logger.Information("Using stream serializer for Stream type");

                // Use recyclable memory stream for better performance
                using var memoryStream = _memoryStreamManager.GetStream("CombinedSerializer-StreamSerialize");

                var streamResult = await _streamSerializer.SerializeAsync(memoryStream, streamValue, cancellationToken)
                    .ConfigureAwait(false);

                if (!streamResult.IsSuccess)
                {
                    return Result<byte[], SerializationError>.Failure(streamResult.Error!);
                }

                var resultBytes = memoryStream.ToArray();

                stopwatch.Stop();
                _logger.Information("Stream serialization completed in {ElapsedMs}ms. Result size: {ResultSize} bytes",
                    stopwatch.ElapsedMilliseconds, resultBytes.Length);

                return Result<byte[], SerializationError>.Success(resultBytes);
            }

            // Use the appropriate serializer based on the type or other criteria
            var useStreamSerializer = ShouldUseStreamSerializer<T>();

            if (useStreamSerializer)
            {
                _logger.Information("Using stream serializer for type {Type}", typeof(T).Name);

                using var memoryStream = _memoryStreamManager.GetStream("CombinedSerializer-Serialize");

                var streamResult = await _streamSerializer.SerializeAsync(memoryStream, value, cancellationToken)
                    .ConfigureAwait(false);

                if (!streamResult.IsSuccess)
                {
                    return Result<byte[], SerializationError>.Failure(streamResult.Error!);
                }

                var resultBytes = memoryStream.ToArray();

                stopwatch.Stop();
                _logger.Information("Stream serialization completed in {ElapsedMs}ms. Result size: {ResultSize} bytes",
                    stopwatch.ElapsedMilliseconds, resultBytes.Length);

                return Result<byte[], SerializationError>.Success(resultBytes);
            }

            _logger.Information("Using default serializer for type {Type}", typeof(T).Name);

            var result = await _defaultSerializer.SerializeAsync(value, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            _logger.Information("Default serialization completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Error occurred during combined serialization for type {Type}: {Message}",
                typeof(T).Name, ex.Message);

            return Result<byte[], SerializationError>.Failure(
                SerializationError.ForType<T>($"Combined serialization error: {ex.Message}", "Serialize"),
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

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Determine if we should use the stream serializer
            var useStreamSerializer = typeof(T) == typeof(Stream) || data.Length > _largeSizeThreshold;

            if (useStreamSerializer)
            {
                _logger.Information("Using stream serializer for type {Type} with data size {DataSize} bytes",
                    typeof(T).Name, data.Length);

                using var memoryStream = _memoryStreamManager.GetStream("CombinedSerializer-Deserialize", data);

                var result = await _streamSerializer.DeserializeAsync<T>(memoryStream, cancellationToken)
                    .ConfigureAwait(false);

                stopwatch.Stop();
                _logger.Information("Stream deserialization completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

                return result;
            }
            else
            {
                _logger.Information("Using default serializer for type {Type} with data size {DataSize} bytes",
                    typeof(T).Name, data.Length);

                var result = await _defaultSerializer.DeserializeAsync<T>(data, cancellationToken)
                    .ConfigureAwait(false);

                stopwatch.Stop();
                _logger.Information("Default deserialization completed in {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);

                return result;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Error occurred during combined deserialization for type {Type}: {Message}",
                typeof(T).Name, ex.Message);

            return Result<T, SerializationError>.Failure(
                SerializationError.ForType<T>($"Combined deserialization error: {ex.Message}", "Deserialize"),
                ex);
        }
    }

    /// <summary>
    ///     Determines whether to use the stream serializer for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if the stream serializer should be used; otherwise, false.</returns>
    private bool ShouldUseStreamSerializer<T>()
    {
        // Stream types should always use the stream serializer
        if (typeof(T) == typeof(Stream) ||
            typeof(Stream).IsAssignableFrom(typeof(T)))
        {
            return true;
        }

        // For large collections, prefer the stream serializer
        if (typeof(T).IsGenericType &&
            (typeof(T).GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
             typeof(T).GetGenericTypeDefinition() == typeof(List<>) ||
             typeof(T).GetGenericTypeDefinition() == typeof(ICollection<>)))
        {
            return true;
        }

        // Check if the stream serializer explicitly supports this type
        if (_streamSerializer is IStreamSerializer serializer && serializer.CanHandleType(typeof(T)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Creates a provider of the specified type from the configuration.
    /// </summary>
    /// <typeparam name="T">The type of provider to create.</typeparam>
    /// <param name="providerType">The type of the provider.</param>
    /// <returns>The created provider, or null if it could not be created.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the provider type is null or no suitable constructor is found.</exception>
    private T? CreateProvider<T>(Type? providerType) where T : class
    {
        if (providerType == null)
        {
            throw new InvalidOperationException($"{typeof(T).Name} type is not specified.");
        }

        try
        {
            var constructor = providerType.GetConstructor([typeof(SerializationConfig)])
                              ?? throw new InvalidOperationException(
                                  $"No suitable constructor found for {providerType.FullName}.");

            return constructor.Invoke([_config]) as T;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create provider of type {ProviderType}: {Message}",
                providerType.Name, ex.Message);
            throw new InvalidOperationException($"Failed to create provider: {ex.Message}", ex);
        }
    }
}
