#region

using System.Diagnostics;
using System.Security.Cryptography;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger<DropBear.Codex.Serialization.Serializers.EncryptedSerializer>;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Serializer that applies encryption to serialized data before serialization and decryption after deserialization.
/// </summary>
public sealed partial class EncryptedSerializer : ISerializer, IDisposable
{
    private readonly int _encryptionThreshold;
    private readonly IEncryptor _encryptor;
    private readonly ISerializer _innerSerializer;
    private readonly ILogger _logger;
    private readonly bool _skipSmallObjects;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EncryptedSerializer" /> class.
    /// </summary>
    /// <param name="innerSerializer">The inner serializer.</param>
    /// <param name="encryptionProvider">The encryption provider to use for encryption and decryption.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="skipSmallObjects">Whether to skip encryption for small objects.</param>
    /// <param name="encryptionThreshold">
    ///     The size threshold in bytes for encryption (objects smaller than this won't be
    ///     encrypted if skipSmallObjects is true).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null.</exception>
    public EncryptedSerializer(
        ISerializer innerSerializer,
        IEncryptionProvider encryptionProvider,
        ILogger logger,
        bool skipSmallObjects = false,
        int encryptionThreshold = 100) // Default to 100 bytes - usually encrypt almost everything
    {
        _innerSerializer = innerSerializer ?? throw new ArgumentNullException(nameof(innerSerializer));
        _encryptor = encryptionProvider?.GetEncryptor() ?? throw new ArgumentNullException(nameof(encryptionProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _skipSmallObjects = skipSmallObjects;
        _encryptionThreshold = encryptionThreshold;

        LogEncryptedSerializerInitialized(_skipSmallObjects, _encryptionThreshold);
    }

    /// <summary>
    ///     Disposes resources used by the serializer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_encryptor is IDisposable disposableEncryptor)
        {
            disposableEncryptor.Dispose();
        }

        if (_innerSerializer is IDisposable disposableSerializer)
        {
            disposableSerializer.Dispose();
        }

        _disposed = true;
    }

    /// <inheritdoc />
    public async Task<Result<byte[], SerializationError>> SerializeAsync<T>(T value,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        LogSerializationStarting(typeof(T).Name);

        try
        {
            // First serialize using the inner serializer
            var serializeResult = await _innerSerializer.SerializeAsync(value, cancellationToken).ConfigureAwait(false);

            if (!serializeResult.IsSuccess)
            {
                return serializeResult;
            }

            var serializedData = serializeResult.Value!;

            // Skip encryption for small objects if configured
            if (_skipSmallObjects && serializedData.Length < _encryptionThreshold)
            {
                LogSkippingEncryptionForSmallObject(typeof(T).Name, serializedData.Length);

                // Add a flag byte to indicate unencrypted data
                var result = new byte[serializedData.Length + 1];
                result[0] = 0; // 0 = unencrypted
                Buffer.BlockCopy(serializedData, 0, result, 1, serializedData.Length);

                stopwatch.Stop();
                LogSerializationCompletedWithoutEncryption(typeof(T).Name, stopwatch.ElapsedMilliseconds);

                return Result<byte[], SerializationError>.Success(result);
            }

            try
            {
                // Apply encryption
                var encryptedResult =
                    await _encryptor.EncryptAsync(serializedData, cancellationToken).ConfigureAwait(false);

                // Short-circuit on error
                if (!encryptedResult.IsSuccess)
                {
                    return Result<byte[], SerializationError>.Failure(encryptedResult.Error!,
                        encryptedResult.Exception);
                }

                // Now we can safely use the .Value property
                var encryptedBytes = encryptedResult.Value!;

                // Add a flag byte
                var resultWithFlag = new byte[encryptedBytes.Length + 1];
                resultWithFlag[0] = 1; // 1 = encrypted
                Buffer.BlockCopy(encryptedBytes, 0, resultWithFlag, 1, encryptedBytes.Length);

                stopwatch.Stop();
                LogSerializationAndEncryptionCompleted(typeof(T).Name, stopwatch.ElapsedMilliseconds,
                    serializedData.Length, encryptedBytes.Length);

                return Result<byte[], SerializationError>.Success(resultWithFlag);
            }
            catch (CryptographicException ex)
            {
                stopwatch.Stop();
                LogCryptographicErrorDuringEncryption(typeof(T).Name, ex.Message, ex);

                return Result<byte[], SerializationError>.Failure(
                    SerializationError.ForType<T>($"Cryptographic error during encryption: {ex.Message}", "Encrypt"),
                    ex);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogSerializationError(typeof(T).Name, ex.Message, ex);

            return Result<byte[], SerializationError>.Failure(
                SerializationError.ForType<T>($"Error during serialization with encryption: {ex.Message}",
                    "SerializeWithEncryption"),
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
                SerializationError.ForType<T>("Cannot deserialize null or empty data", "DeserializeWithDecryption"));
        }

        var stopwatch = Stopwatch.StartNew();
        LogDeserializationStarting(typeof(T).Name);

        try
        {
            // Check the flag byte
            var isEncrypted = data[0] == 1;

            // Extract the actual data (skipping the flag byte)
            var actualData = new byte[data.Length - 1];
            Buffer.BlockCopy(data, 1, actualData, 0, data.Length - 1);

            byte[] dataToDeserialize;

            if (isEncrypted)
            {
                try
                {
                    var decryptResult =
                        await _encryptor.DecryptAsync(actualData, cancellationToken).ConfigureAwait(false);
                    if (!decryptResult.IsSuccess)
                    {
                        return Result<T, SerializationError>.Failure(decryptResult.Error!, decryptResult.Exception);
                    }

                    dataToDeserialize = decryptResult.Value!;


                    LogDecryptionCompleted(actualData.Length, dataToDeserialize.Length);
                }
                catch (CryptographicException ex)
                {
                    stopwatch.Stop();
                    LogCryptographicErrorDuringDecryption(typeof(T).Name, ex.Message, ex);

                    return Result<T, SerializationError>.Failure(
                        SerializationError.ForType<T>($"Cryptographic error during decryption: {ex.Message}",
                            "Decrypt"),
                        ex);
                }
            }
            else
            {
                // Data wasn't encrypted
                dataToDeserialize = actualData;
                LogDataWasNotEncrypted(dataToDeserialize.Length);
            }

            // Deserialize the data using the inner serializer
            var deserializeResult = await _innerSerializer.DeserializeAsync<T>(dataToDeserialize, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            if (deserializeResult.IsSuccess)
            {
                LogDeserializationCompletedSuccessfully(typeof(T).Name, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                LogDeserializationFailed(typeof(T).Name, stopwatch.ElapsedMilliseconds,
                    deserializeResult.Error?.Message ?? "Unknown error");
            }

            return deserializeResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogDeserializationError(typeof(T).Name, ex.Message, ex);

            return Result<T, SerializationError>.Failure(
                SerializationError.ForType<T>($"Error during deserialization with decryption: {ex.Message}",
                    "DeserializeWithDecryption"),
                ex);
        }
    }

    /// <inheritdoc />
    public Dictionary<string, object> GetCapabilities()
    {
        var innerCapabilities = _innerSerializer.GetCapabilities();

        var capabilities = new Dictionary<string, object>(innerCapabilities, StringComparer.Ordinal)
        {
            ["EncryptionEnabled"] = true,
            ["SkipSmallObjects"] = _skipSmallObjects,
            ["EncryptionThreshold"] = _encryptionThreshold,
            ["EncryptorType"] = _encryptor.GetType().Name
        };

        return capabilities;
    }

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "EncryptedSerializer initialized with SkipSmallObjects: {SkipSmallObjects}, EncryptionThreshold: {EncryptionThreshold} bytes")]
    partial void LogEncryptedSerializerInitialized(bool skipSmallObjects, int encryptionThreshold);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting serialization and encryption of type {Type}")]
    partial void LogSerializationStarting(string type);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Skipping encryption for small object of type {Type} with size {Size} bytes")]
    partial void LogSkippingEncryptionForSmallObject(string type, int size);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Serialization of type {Type} completed without encryption in {ElapsedMs}ms")]
    partial void LogSerializationCompletedWithoutEncryption(string type, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "Serialization and encryption of type {Type} completed in {ElapsedMs}ms. Original size: {OriginalSize} bytes, Encrypted size: {EncryptedSize} bytes")]
    partial void LogSerializationAndEncryptionCompleted(string type, long elapsedMs, int originalSize,
        int encryptedSize);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Cryptographic error during encryption of type {Type}: {Message}")]
    partial void LogCryptographicErrorDuringEncryption(string type, string message, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error occurred during serialization and encryption of type {Type}: {Message}")]
    partial void LogSerializationError(string type, string message, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting decryption and deserialization of type {Type}")]
    partial void LogDeserializationStarting(string type);

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "Decryption completed. Encrypted size: {EncryptedSize} bytes, Decrypted size: {DecryptedSize} bytes")]
    partial void LogDecryptionCompleted(int encryptedSize, int decryptedSize);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Cryptographic error during decryption for type {Type}: {Message}")]
    partial void LogCryptographicErrorDuringDecryption(string type, string message, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Data was not encrypted. Size: {Size} bytes")]
    partial void LogDataWasNotEncrypted(int size);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Decryption and deserialization of type {Type} completed successfully in {ElapsedMs}ms")]
    partial void LogDeserializationCompletedSuccessfully(string type, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Deserialization of type {Type} failed after decryption in {ElapsedMs}ms: {Error}")]
    partial void LogDeserializationFailed(string type, long elapsedMs, string error);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error occurred during decryption and deserialization of type {Type}: {Message}")]
    partial void LogDeserializationError(string type, string message, Exception ex);

    #endregion
}
