#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Serializer that applies encryption to serialized data before serialization and decryption after deserialization.
/// </summary>
public class EncryptedSerializer : ISerializer
{
    private readonly IEncryptor _encryptor;
    private readonly ISerializer _innerSerializer;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<EncryptedSerializer>();

    /// <summary>
    ///     Initializes a new instance of the <see cref="EncryptedSerializer" /> class.
    /// </summary>
    /// <param name="innerSerializer">The inner serializer.</param>
    /// <param name="encryptionProvider">The encryption provider to use for encryption and decryption.</param>
    public EncryptedSerializer(ISerializer innerSerializer, IEncryptionProvider? encryptionProvider)
    {
        _innerSerializer = innerSerializer ?? throw new ArgumentNullException(nameof(innerSerializer));
        _encryptor = encryptionProvider?.GetEncryptor() ?? throw new ArgumentNullException(nameof(encryptionProvider));
    }

    /// <inheritdoc />
    public async Task<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Information("Starting serialization and encryption of type {Type}.", typeof(T));

            // Serialize the value using the inner serializer
            var serializedData = await _innerSerializer.SerializeAsync(value, cancellationToken).ConfigureAwait(false);

            // Encrypt the serialized data using the provided encryptor
            var encryptedData = await _encryptor.EncryptAsync(serializedData, cancellationToken).ConfigureAwait(false);

            _logger.Information("Serialization and encryption of type {Type} completed successfully.", typeof(T));

            return encryptedData;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during serialization and encryption of type {Type}.", typeof(T));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Information("Starting decryption and deserialization of type {Type}.", typeof(T));

            // Decrypt the data using the provided encryptor
            var decryptedData = await _encryptor.DecryptAsync(data, cancellationToken).ConfigureAwait(false);

            // Deserialize the decrypted data using the inner serializer
            var deserializedObject = await _innerSerializer.DeserializeAsync<T>(decryptedData, cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Decryption and deserialization of type {Type} completed successfully.", typeof(T));

            return deserializedObject;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during decryption and deserialization of type {Type}.", typeof(T));
            throw;
        }
    }
}
