#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Serializer that applies compression to serialized data before serialization and decompression after
///     deserialization.
/// </summary>
public class CompressedSerializer : ISerializer
{
    private readonly ICompressor _compressor;
    private readonly ISerializer _innerSerializer;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<CompressedSerializer>();

    /// <summary>
    ///     Initializes a new instance of the <see cref="CompressedSerializer" /> class.
    /// </summary>
    /// <param name="innerSerializer">The inner serializer.</param>
    /// <param name="compressionProvider">The compression provider to use for compression and decompression.</param>
    public CompressedSerializer(ISerializer innerSerializer, ICompressionProvider? compressionProvider)
    {
        _innerSerializer = innerSerializer ?? throw new ArgumentNullException(nameof(innerSerializer));
        _compressor = compressionProvider?.GetCompressor() ??
                      throw new ArgumentNullException(nameof(compressionProvider));
    }

    /// <inheritdoc />
    public async Task<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Information("Starting serialization of type {Type} with compression.", typeof(T));

            var serializedData = await _innerSerializer.SerializeAsync(value, cancellationToken).ConfigureAwait(false);
            var compressedData =
                await _compressor.CompressAsync(serializedData, cancellationToken).ConfigureAwait(false);

            _logger.Information("Serialization and compression of type {Type} completed successfully.", typeof(T));

            return compressedData;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during serialization and compression of type {Type}.", typeof(T));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Information("Starting decompression and deserialization of type {Type}.", typeof(T));

            var decompressedData = await _compressor.DecompressAsync(data, cancellationToken).ConfigureAwait(false);
            var deserializedObject = await _innerSerializer.DeserializeAsync<T>(decompressedData, cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Decompression and deserialization of type {Type} completed successfully.", typeof(T));

            return deserializedObject;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during decompression and deserialization of type {Type}.", typeof(T));
            throw;
        }
    }
}
