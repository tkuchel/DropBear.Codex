#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Serializer that applies encoding to serialized data before serialization and decoding after deserialization.
/// </summary>
public class EncodedSerializer : ISerializer
{
    private readonly IEncoder _encoder;
    private readonly ISerializer _innerSerializer;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<EncodedSerializer>();

    /// <summary>
    ///     Initializes a new instance of the <see cref="EncodedSerializer" /> class.
    /// </summary>
    /// <param name="innerSerializer">The inner serializer.</param>
    /// <param name="encodingProvider">The encoding provider to use for encoding and decoding.</param>
    public EncodedSerializer(ISerializer innerSerializer, IEncodingProvider encodingProvider)
    {
        _innerSerializer = innerSerializer ?? throw new ArgumentNullException(nameof(innerSerializer));
        _encoder = encodingProvider?.GetEncoder() ?? throw new ArgumentNullException(nameof(encodingProvider));
    }

    /// <inheritdoc />
    public async Task<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Information("Starting serialization of type {Type} with encoding.", typeof(T));

            // Serialize the value using the inner serializer
            var serializedData = await _innerSerializer.SerializeAsync(value, cancellationToken).ConfigureAwait(false);

            // Encode the serialized data using the provided encoder
            var encodedData = await _encoder.EncodeAsync(serializedData, cancellationToken).ConfigureAwait(false);

            _logger.Information("Serialization and encoding of type {Type} completed successfully.", typeof(T));

            return encodedData;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during serialization and encoding of type {Type}.", typeof(T));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Information("Starting decoding and deserialization of type {Type}.", typeof(T));

            // Decode the data using the provided encoder
            var decodedData = await _encoder.DecodeAsync(data, cancellationToken).ConfigureAwait(false);

            // Deserialize the decoded data using the inner serializer
            var deserializedObject = await _innerSerializer.DeserializeAsync<T>(decodedData, cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Decoding and deserialization of type {Type} completed successfully.", typeof(T));

            return deserializedObject;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during decoding and deserialization of type {Type}.", typeof(T));
            throw;
        }
    }
}
