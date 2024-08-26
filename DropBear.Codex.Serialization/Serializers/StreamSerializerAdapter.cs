#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Adapter that allows an IStreamSerializer to be used where an ISerializer is expected.
/// </summary>
public class StreamSerializerAdapter : ISerializer
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<StreamSerializerAdapter>();
    private readonly IStreamSerializer _streamSerializer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamSerializerAdapter" /> class.
    /// </summary>
    /// <param name="streamSerializer">The stream serializer to adapt.</param>
    public StreamSerializerAdapter(IStreamSerializer streamSerializer)
    {
        _streamSerializer = streamSerializer ?? throw new ArgumentNullException(nameof(streamSerializer));
    }

    /// <inheritdoc />
    public async Task<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        _logger.Information("Starting stream-based serialization for type {Type}.", typeof(T));

        try
        {
            using var memoryStream = new MemoryStream();
            await _streamSerializer.SerializeAsync(memoryStream, value, cancellationToken).ConfigureAwait(false);

            _logger.Information("Stream-based serialization completed successfully for type {Type}.", typeof(T));
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during stream-based serialization for type {Type}.", typeof(T));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
    {
        _logger.Information("Starting stream-based deserialization for type {Type}.", typeof(T));

        try
        {
            using var memoryStream = new MemoryStream(data);
            var result = await _streamSerializer.DeserializeAsync<T>(memoryStream, cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Stream-based deserialization completed successfully for type {Type}.", typeof(T));
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during stream-based deserialization for type {Type}.", typeof(T));
            throw;
        }
    }
}
