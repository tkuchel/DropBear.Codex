#region

using System.Diagnostics;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Serializer that applies compression to serialized data before serialization and decompression after
///     deserialization.
/// </summary>
public sealed class CompressedSerializer : ISerializer
{
    private readonly int _compressionThreshold;
    private readonly ICompressor _compressor;
    private readonly ISerializer _innerSerializer;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<CompressedSerializer>();
    private readonly bool _skipSmallObjects;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CompressedSerializer" /> class.
    /// </summary>
    /// <param name="innerSerializer">The inner serializer.</param>
    /// <param name="compressionProvider">The compression provider to use for compression and decompression.</param>
    /// <param name="skipSmallObjects">Whether to skip compression for small objects.</param>
    /// <param name="compressionThreshold">
    ///     The size threshold in bytes for compression (objects smaller than this won't be
    ///     compressed if skipSmallObjects is true).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null.</exception>
    public CompressedSerializer(
        ISerializer innerSerializer,
        ICompressionProvider compressionProvider,
        bool skipSmallObjects = true,
        int compressionThreshold = 1024) // Default to 1KB
    {
        _innerSerializer = innerSerializer ?? throw new ArgumentNullException(nameof(innerSerializer));
        _compressor = compressionProvider?.GetCompressor() ??
                      throw new ArgumentNullException(nameof(compressionProvider));
        _skipSmallObjects = skipSmallObjects;
        _compressionThreshold = compressionThreshold;

        _logger.Information("CompressedSerializer initialized with SkipSmallObjects: {SkipSmallObjects}, " +
                            "CompressionThreshold: {CompressionThreshold} bytes",
            _skipSmallObjects, _compressionThreshold);
    }

    /// <inheritdoc />
    public async Task<Result<byte[], SerializationError>> SerializeAsync<T>(T value,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.Information("Starting serialization of type {Type} with compression.", typeof(T).Name);

        try
        {
            // First serialize using the inner serializer
            var serializeResult = await _innerSerializer.SerializeAsync(value, cancellationToken).ConfigureAwait(false);

            if (!serializeResult.IsSuccess)
            {
                return serializeResult;
            }

            var serializedData = serializeResult.Value!;

            // Skip compression for small objects if configured
            if (_skipSmallObjects && serializedData.Length < _compressionThreshold)
            {
                _logger.Information("Skipping compression for small object of type {Type} with size {Size} bytes.",
                    typeof(T).Name, serializedData.Length);

                // Add a flag byte to indicate uncompressed data
                var result = new byte[serializedData.Length + 1];
                result[0] = 0; // 0 = uncompressed
                Buffer.BlockCopy(serializedData, 0, result, 1, serializedData.Length);

                stopwatch.Stop();
                _logger.Information("Serialization of type {Type} completed without compression in {ElapsedMs}ms.",
                    typeof(T).Name, stopwatch.ElapsedMilliseconds);

                return Result<byte[], SerializationError>.Success(result);
            }

            // Apply compression
            var compressResult =
                await _compressor.CompressAsync(serializedData, cancellationToken).ConfigureAwait(false);

            if (!compressResult.IsSuccess)
            {
                return Result<byte[], SerializationError>.Failure(compressResult.Error!);
            }

            var compressedData = compressResult.Value!;

            // Add a flag byte to indicate compressed data
            var resultWithFlag = new byte[compressedData.Length + 1];
            resultWithFlag[0] = 1; // 1 = compressed
            Buffer.BlockCopy(compressedData, 0, resultWithFlag, 1, compressedData.Length);

            stopwatch.Stop();

            _logger.Information("Serialization and compression of type {Type} completed in {ElapsedMs}ms. " +
                                "Original size: {OriginalSize} bytes, Compressed size: {CompressedSize} bytes, Ratio: {CompressionRatio:P2}.",
                typeof(T).Name, stopwatch.ElapsedMilliseconds, serializedData.Length, compressedData.Length,
                (float)compressedData.Length / serializedData.Length);

            return Result<byte[], SerializationError>.Success(resultWithFlag);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Error occurred during serialization and compression of type {Type}: {Message}",
                typeof(T).Name, ex.Message);

            return Result<byte[], SerializationError>.Failure(
                SerializationError.ForType<T>($"Error during serialization with compression: {ex.Message}",
                    "SerializeWithCompression"),
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
                SerializationError.ForType<T>("Cannot deserialize null or empty data", "DeserializeWithDecompression"));
        }

        var stopwatch = Stopwatch.StartNew();
        _logger.Information("Starting decompression and deserialization of type {Type}.", typeof(T).Name);

        try
        {
            // Check the flag byte
            var isCompressed = data[0] == 1;

            // Extract the actual data (skipping the flag byte)
            var actualData = new byte[data.Length - 1];
            Buffer.BlockCopy(data, 1, actualData, 0, data.Length - 1);

            byte[] dataToDeserialize;

            if (isCompressed)
            {
                // Decompress the data
                var decompressResult =
                    await _compressor.DecompressAsync(actualData, cancellationToken).ConfigureAwait(false);

                if (!decompressResult.IsSuccess)
                {
                    return Result<T, SerializationError>.Failure(decompressResult.Error!);
                }

                dataToDeserialize = decompressResult.Value!;

                _logger.Information("Decompression completed. Compressed size: {CompressedSize} bytes, " +
                                    "Decompressed size: {DecompressedSize} bytes",
                    actualData.Length, dataToDeserialize.Length);
            }
            else
            {
                // Data wasn't compressed
                dataToDeserialize = actualData;
                _logger.Information("Data was not compressed. Size: {Size} bytes", dataToDeserialize.Length);
            }

            // Deserialize the data using the inner serializer
            var deserializeResult = await _innerSerializer.DeserializeAsync<T>(dataToDeserialize, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            if (deserializeResult.IsSuccess)
            {
                _logger.Information(
                    "Decompression and deserialization of type {Type} completed successfully in {ElapsedMs}ms.",
                    typeof(T).Name, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.Warning("Deserialization of type {Type} failed after decompression in {ElapsedMs}ms: {Error}",
                    typeof(T).Name, stopwatch.ElapsedMilliseconds, deserializeResult.Error?.Message);
            }

            return deserializeResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Error occurred during decompression and deserialization of type {Type}: {Message}",
                typeof(T).Name, ex.Message);

            return Result<T, SerializationError>.Failure(
                SerializationError.ForType<T>($"Error during deserialization with decompression: {ex.Message}",
                    "DeserializeWithDecompression"),
                ex);
        }
    }

    /// <inheritdoc />
    public Dictionary<string, object> GetCapabilities()
    {
        var innerCapabilities = _innerSerializer.GetCapabilities();
        var compressorInfo = _compressor.GetCompressionInfo();

        var capabilities = new Dictionary<string, object>(innerCapabilities, StringComparer.Ordinal)
        {
            ["CompressionEnabled"] = true,
            ["SkipSmallObjects"] = _skipSmallObjects,
            ["CompressionThreshold"] = _compressionThreshold
        };

        // Add compressor info with "Compression" prefix to avoid key conflicts
        foreach (var kvp in compressorInfo)
        {
            capabilities[$"Compression{kvp.Key}"] = kvp.Value;
        }

        return capabilities;
    }
}
