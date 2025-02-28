#region

using System.Buffers;
using System.IO.Compression;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Compression;

/// <summary>
///     Provides methods to compress and decompress data using the Deflate algorithm.
/// </summary>
public sealed class DeflateCompressor : ICompressor
{
    private readonly int _bufferSize;
    private readonly CompressionLevel _compressionLevel;
    private readonly ILogger _logger;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DeflateCompressor" /> class with default settings.
    /// </summary>
    public DeflateCompressor()
        : this(CompressionLevel.Fastest, 81920) // Default to fastest compression and 80KB buffer
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DeflateCompressor" /> class with specified settings.
    /// </summary>
    /// <param name="compressionLevel">The compression level to use.</param>
    /// <param name="bufferSize">The buffer size for compression operations.</param>
    public DeflateCompressor(CompressionLevel compressionLevel, int bufferSize)
    {
        _compressionLevel = compressionLevel;
        _bufferSize = bufferSize > 0 ? bufferSize : 81920; // Default to 80KB if invalid
        _memoryStreamManager = new RecyclableMemoryStreamManager();
        _logger = LoggerFactory.Logger.ForContext<DeflateCompressor>();

        _logger.Information(
            "DeflateCompressor initialized with CompressionLevel: {CompressionLevel}, BufferSize: {BufferSize}",
            compressionLevel, _bufferSize);
    }

    /// <inheritdoc />
    public async Task<Result<byte[], SerializationError>> CompressAsync(byte[] data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(data, nameof(data));

            if (data.Length == 0)
            {
                return Result<byte[], SerializationError>.Success(Array.Empty<byte>());
            }

            _logger.Information("Starting compression of data with length {DataLength} bytes.", data.Length);

            using var compressedStream = _memoryStreamManager.GetStream("DeflateCompressor-Compress");

            // Use braced scope to ensure proper disposal of resources
            {
                using var deflateStream =
                    new DeflateStream(compressedStream, _compressionLevel, true); // Leave stream open

                // Use Memory<T> for better performance
                await deflateStream.WriteAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
                await deflateStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            } // deflateStream is disposed here, ensuring all data is written to compressedStream

            // Reset position to read the compressed data
            compressedStream.Position = 0;

            var result = compressedStream.ToArray();

            _logger.Information(
                "Compression completed. Data compressed from {OriginalSize} to {CompressedSize} bytes. Ratio: {Ratio:P2}",
                data.Length, result.Length, (float)result.Length / data.Length);

            return Result<byte[], SerializationError>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred while compressing data: {Message}", ex.Message);
            return Result<byte[], SerializationError>.Failure(
                new SerializationError($"Deflate compression failed: {ex.Message}") { Operation = "Compress" }, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<byte[], SerializationError>> DecompressAsync(byte[] compressedData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(compressedData, nameof(compressedData));

            if (compressedData.Length == 0)
            {
                return Result<byte[], SerializationError>.Success(Array.Empty<byte>());
            }

            _logger.Information("Starting decompression of data with length {CompressedDataLength} bytes.",
                compressedData.Length);

            // Create input stream with compressed data
            using var compressedStream =
                _memoryStreamManager.GetStream("DeflateCompressor-Decompress-Input", compressedData);

            // Create output stream for decompressed data
            using var decompressedStream = _memoryStreamManager.GetStream("DeflateCompressor-Decompress-Output");

            // Use a shared buffer from the array pool to minimize allocations
            var buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);

            try
            {
                using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);

                int bytesRead;
                while ((bytesRead = await deflateStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                           .ConfigureAwait(false)) > 0)
                {
                    await decompressedStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                        .ConfigureAwait(false);
                }

                await decompressedStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Return the buffer to the pool when done
                ArrayPool<byte>.Shared.Return(buffer);
            }

            // Reset position to read the decompressed content
            decompressedStream.Position = 0;

            var result = decompressedStream.ToArray();

            _logger.Information(
                "Decompression completed. Data decompressed from {CompressedSize} to {DecompressedSize} bytes.",
                compressedData.Length, result.Length);

            return Result<byte[], SerializationError>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred while decompressing data: {Message}", ex.Message);
            return Result<byte[], SerializationError>.Failure(
                new SerializationError($"Deflate decompression failed: {ex.Message}") { Operation = "Decompress" }, ex);
        }
    }

    /// <inheritdoc />
    public IDictionary<string, object> GetCompressionInfo()
    {
        return new Dictionary<string, object>
(StringComparer.Ordinal)
        {
            ["Algorithm"] = "Deflate",
            ["CompressionLevel"] = _compressionLevel.ToString(),
            ["BufferSize"] = _bufferSize,
            ["IsThreadSafe"] = true
        };
    }
}
