#region

using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger<DropBear.Codex.Serialization.Serializers.EncodedSerializer>;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Serializer that applies encoding to serialized data before serialization and decoding after deserialization.
/// </summary>
public sealed partial class EncodedSerializer : ISerializer
{
    private readonly IEncoder _encoder;
    private readonly int _encodingThreshold;
    private readonly ISerializer _innerSerializer;
    private readonly ILogger _logger;
    private readonly bool _skipLargeObjects;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EncodedSerializer" /> class.
    /// </summary>
    /// <param name="innerSerializer">The inner serializer.</param>
    /// <param name="encodingProvider">The encoding provider to use for encoding and decoding.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="skipLargeObjects">Whether to skip encoding for large objects.</param>
    /// <param name="encodingThreshold">
    ///     The size threshold in bytes for encoding (objects larger than this won't be encoded if
    ///     skipLargeObjects is true).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null.</exception>
    public EncodedSerializer(
        ISerializer innerSerializer,
        IEncodingProvider encodingProvider,
        ILogger logger,
        bool skipLargeObjects = true,
        int encodingThreshold = 1024 * 1024) // Default to 1MB - skip large objects for performance
    {
        _innerSerializer = innerSerializer ?? throw new ArgumentNullException(nameof(innerSerializer));
        _encoder = encodingProvider?.GetEncoder() ?? throw new ArgumentNullException(nameof(encodingProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _skipLargeObjects = skipLargeObjects;
        _encodingThreshold = encodingThreshold;

        LogEncodedSerializerInitialized(_logger, _skipLargeObjects, _encodingThreshold);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> GetCapabilities()
    {
        var innerCapabilities = _innerSerializer.GetCapabilities();
        var encoderInfo = _encoder.GetEncoderInfo();

        var capabilities = new Dictionary<string, object>(innerCapabilities, StringComparer.Ordinal)
        {
            ["EncodingEnabled"] = true,
            ["SkipLargeObjects"] = _skipLargeObjects,
            ["EncodingThreshold"] = _encodingThreshold
        };

        // Add encoder info with "Encoding" prefix to avoid key conflicts
        foreach (var kvp in encoderInfo)
        {
            capabilities[$"Encoding{kvp.Key}"] = kvp.Value;
        }

        return capabilities;
    }

    /// <inheritdoc />
    public async Task<Result<byte[], SerializationError>> SerializeAsync<T>(T value,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        LogSerializationStarting(_logger, typeof(T).Name);

        try
        {
            // First serialize using the inner serializer
            var serializeResult = await _innerSerializer.SerializeAsync(value, cancellationToken).ConfigureAwait(false);

            if (!serializeResult.IsSuccess)
            {
                return serializeResult;
            }

            var serializedData = serializeResult.Value!;

            // Skip encoding for large objects if configured
            if (_skipLargeObjects && serializedData.Length > _encodingThreshold)
            {
                LogSkippingEncodingForLargeObject(_logger, typeof(T).Name, serializedData.Length);

                // Add a flag byte to indicate unencoded data
                var result = new byte[serializedData.Length + 1];
                result[0] = 0; // 0 = unencoded
                Buffer.BlockCopy(serializedData, 0, result, 1, serializedData.Length);

                stopwatch.Stop();
                LogSerializationCompletedWithoutEncoding(_logger, typeof(T).Name, stopwatch.ElapsedMilliseconds);

                return Result<byte[], SerializationError>.Success(result);
            }

            // Apply encoding
            var encodeResult = await _encoder.EncodeAsync(serializedData, cancellationToken).ConfigureAwait(false);

            if (!encodeResult.IsSuccess)
            {
                return Result<byte[], SerializationError>.Failure(encodeResult.Error!);
            }

            var encodedData = encodeResult.Value!;

            // Add a flag byte to indicate encoded data
            var resultWithFlag = new byte[encodedData.Length + 1];
            resultWithFlag[0] = 1; // 1 = encoded
            Buffer.BlockCopy(encodedData, 0, resultWithFlag, 1, encodedData.Length);

            stopwatch.Stop();

            LogSerializationAndEncodingCompleted(_logger, typeof(T).Name, stopwatch.ElapsedMilliseconds,
                serializedData.Length, encodedData.Length, (float)encodedData.Length / serializedData.Length);

            return Result<byte[], SerializationError>.Success(resultWithFlag);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogSerializationError(_logger, typeof(T).Name, ex.Message, ex);

            return Result<byte[], SerializationError>.Failure(
                SerializationError.ForType<T>($"Error during serialization with encoding: {ex.Message}",
                    "SerializeWithEncoding"),
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
                SerializationError.ForType<T>("Cannot deserialize null or empty data", "DeserializeWithDecoding"));
        }

        var stopwatch = Stopwatch.StartNew();
        LogDeserializationStarting(_logger, typeof(T).Name);

        try
        {
            // Check the flag byte
            var isEncoded = data[0] == 1;

            // Extract the actual data (skipping the flag byte)
            var actualData = new byte[data.Length - 1];
            Buffer.BlockCopy(data, 1, actualData, 0, data.Length - 1);

            byte[] dataToDeserialize;

            if (isEncoded)
            {
                // Decode the data
                var decodeResult = await _encoder.DecodeAsync(actualData, cancellationToken).ConfigureAwait(false);

                if (!decodeResult.IsSuccess)
                {
                    return Result<T, SerializationError>.Failure(decodeResult.Error!);
                }

                dataToDeserialize = decodeResult.Value!;

                LogDecodingCompleted(_logger, actualData.Length, dataToDeserialize.Length);
            }
            else
            {
                // Data wasn't encoded
                dataToDeserialize = actualData;
                LogDataWasNotEncoded(_logger, dataToDeserialize.Length);
            }

            // Deserialize the data using the inner serializer
            var deserializeResult = await _innerSerializer.DeserializeAsync<T>(dataToDeserialize, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            if (deserializeResult.IsSuccess)
            {
                LogDeserializationCompletedSuccessfully(_logger, typeof(T).Name, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                LogDeserializationFailed(_logger, typeof(T).Name, stopwatch.ElapsedMilliseconds,
                    deserializeResult.Error?.Message ?? "Unknown error");
            }

            return deserializeResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogDeserializationError(_logger, typeof(T).Name, ex.Message, ex);

            return Result<T, SerializationError>.Failure(
                SerializationError.ForType<T>($"Error during deserialization with decoding: {ex.Message}",
                    "DeserializeWithDecoding"),
                ex);
        }
    }

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "EncodedSerializer initialized with SkipLargeObjects: {SkipLargeObjects}, EncodingThreshold: {EncodingThreshold} bytes")]
    static partial void LogEncodedSerializerInitialized(ILogger logger, bool skipLargeObjects, int encodingThreshold);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting serialization of type {Type} with encoding")]
    static partial void LogSerializationStarting(ILogger logger, string type);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Skipping encoding for large object of type {Type} with size {Size} bytes")]
    static partial void LogSkippingEncodingForLargeObject(ILogger logger, string type, int size);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Serialization of type {Type} completed without encoding in {ElapsedMs}ms")]
    static partial void LogSerializationCompletedWithoutEncoding(ILogger logger, string type, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "Serialization and encoding of type {Type} completed in {ElapsedMs}ms. Original size: {OriginalSize} bytes, Encoded size: {EncodedSize} bytes, Ratio: {EncodingRatio:P2}")]
    static partial void LogSerializationAndEncodingCompleted(ILogger logger, string type, long elapsedMs, int originalSize,
        int encodedSize, float encodingRatio);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error occurred during serialization and encoding of type {Type}: {Message}")]
    static partial void LogSerializationError(ILogger logger, string type, string message, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting decoding and deserialization of type {Type}")]
    static partial void LogDeserializationStarting(ILogger logger, string type);

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "Decoding completed. Encoded size: {EncodedSize} bytes, Decoded size: {DecodedSize} bytes")]
    static partial void LogDecodingCompleted(ILogger logger, int encodedSize, int decodedSize);

    [LoggerMessage(Level = LogLevel.Information, Message = "Data was not encoded. Size: {Size} bytes")]
    static partial void LogDataWasNotEncoded(ILogger logger, int size);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Decoding and deserialization of type {Type} completed successfully in {ElapsedMs}ms")]
    static partial void LogDeserializationCompletedSuccessfully(ILogger logger, string type, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Deserialization of type {Type} failed after decoding in {ElapsedMs}ms: {Error}")]
    static partial void LogDeserializationFailed(ILogger logger, string type, long elapsedMs, string error);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error occurred during decoding and deserialization of type {Type}: {Message}")]
    static partial void LogDeserializationError(ILogger logger, string type, string message, Exception ex);

    #endregion
}
