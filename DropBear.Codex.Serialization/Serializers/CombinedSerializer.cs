#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     A serializer that combines both regular and stream-based serialization strategies.
///     It chooses the appropriate strategy based on the context or type of the data.
/// </summary>
public class CombinedSerializer : ISerializer
{
    private const int LargeSizeThreshold = 1024 * 1024 * 10; // 10 MB threshold
    private readonly SerializationConfig _config;
    private readonly ISerializer _defaultSerializer;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<CombinedSerializer>();
    private readonly IStreamSerializer _streamSerializer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CombinedSerializer" /> class.
    /// </summary>
    public CombinedSerializer(SerializationConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _defaultSerializer = CreateProvider<ISerializer>(config.SerializerType)
                             ?? throw new InvalidOperationException("Default serializer must implement ISerializer.");

        _streamSerializer = CreateProvider<IStreamSerializer>(config.StreamSerializerType)
                            ?? throw new InvalidOperationException(
                                "Stream serializer must implement IStreamSerializer.");
    }

    /// <summary>
    ///     Serializes an object asynchronously, using either the default serializer or the stream serializer depending on the
    ///     type of the value.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation and returns the serialized data as a byte array.</returns>
    public async Task<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        if (value is Stream streamValue)
        {
            _logger.Information("Using stream serializer for type {Type}", typeof(T));
            var memoryStream = new MemoryStream();
            await _streamSerializer.SerializeAsync(memoryStream, streamValue, cancellationToken).ConfigureAwait(false);
            return memoryStream.ToArray();
        }

        _logger.Information("Using default serializer for type {Type}", typeof(T));
        return await _defaultSerializer.SerializeAsync(value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deserializes data asynchronously, using either the default serializer or the stream serializer depending on the
    ///     type of the data.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
    /// <param name="data">The data to deserialize.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation and returns the deserialized object.</returns>
    public async Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
    {
        if (typeof(T) == typeof(Stream) || data.Length > LargeSizeThreshold)
        {
            _logger.Information("Using stream serializer for type {Type}", typeof(T));
            var memoryStream = new MemoryStream(data);
            return await _streamSerializer.DeserializeAsync<T>(memoryStream, cancellationToken).ConfigureAwait(false);
        }

        _logger.Information("Using default serializer for type {Type}", typeof(T));
        return await _defaultSerializer.DeserializeAsync<T>(data, cancellationToken).ConfigureAwait(false);
    }

    private T? CreateProvider<T>(Type? providerType) where T : class
    {
        if (providerType == null)
        {
            throw new InvalidOperationException($"{typeof(T).Name} type is not specified.");
        }

        var constructor = providerType.GetConstructor([typeof(SerializationConfig)])
                          ?? throw new InvalidOperationException(
                              $"No suitable constructor found for {providerType.FullName}.");

        return constructor.Invoke([_config]) as T;
    }
}
