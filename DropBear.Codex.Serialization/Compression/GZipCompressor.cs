#region

using System.Buffers;
using System.IO.Compression;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.IO;

#endregion

namespace DropBear.Codex.Serialization.Compression;

/// <summary>
///     Provides methods to compress and decompress data using the GZip algorithm.
/// </summary>
public sealed partial class GZipCompressor : ICompressor
{
    private readonly int _bufferSize;
    private readonly CompressionLevel _compressionLevel;
    private readonly ILogger<GZipCompressor> _logger;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GZipCompressor" /> class with default settings.
    /// </summary>
    public GZipCompressor(ILogger<GZipCompressor> logger)
        : this(CompressionLevel.Fastest, 81920, logger) // Default to fastest compression and 80KB buffer
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="GZipCompressor" /> class with specified settings.
    /// </summary>
    /// <param name="compressionLevel">The compression level to use.</param>
    /// <param name="bufferSize">The buffer size for compression operations.</param>
    /// <param name="logger">The logger instance.</param>
    public GZipCompressor(CompressionLevel compressionLevel, int bufferSize, ILogger<GZipCompressor> logger)
    {
        _logger = logger;
        _compressionLevel = compressionLevel;
        _bufferSize = bufferSize > 0 ? bufferSize : 81920; // Default to 80KB if invalid
        _memoryStreamManager = new RecyclableMemoryStreamManager();

        LogCompressorInitialized(compressionLevel.ToString(), _bufferSize);
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
                return Result<byte[], SerializationError>.Success([]);
            }

            LogCompressionStarting(data.Length);

            using var compressedStream = _memoryStreamManager.GetStream("GZipCompressor-Compress");

            // Use braced scope to ensure proper disposal of resources
            {
                using var zipStream = new GZipStream(compressedStream, _compressionLevel, true); // Leave stream open

                // Use Memory<T> for better performance
                await zipStream.WriteAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
                await zipStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            } // zipStream is disposed here, ensuring all data is written to compressedStream

            // Reset position to read the compressed data
            compressedStream.Position = 0;

            var result = compressedStream.ToArray();

            LogCompressionCompleted(data.Length, result.Length, (float)result.Length / data.Length);

            return Result<byte[], SerializationError>.Success(result);
        }
        catch (Exception ex)
        {
            LogCompressionError(ex, ex.Message);
            return Result<byte[], SerializationError>.Failure(
                new SerializationError($"GZip compression failed: {ex.Message}") { Operation = "Compress" }, ex);
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
                return Result<byte[], SerializationError>.Success([]);
            }

            LogDecompressionStarting(compressedData.Length);

            // Create input stream with compressed data
            using var compressedStream =
                _memoryStreamManager.GetStream("GZipCompressor-Decompress-Input", compressedData);

            // Create output stream for decompressed data
            using var decompressedStream = _memoryStreamManager.GetStream("GZipCompressor-Decompress-Output");

            // Use a shared buffer from the array pool to minimize allocations
            var buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);

            try
            {
                using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);

                int bytesRead;
                while ((bytesRead = await zipStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
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

            LogDecompressionCompleted(compressedData.Length, result.Length);

            return Result<byte[], SerializationError>.Success(result);
        }
        catch (Exception ex)
        {
            LogDecompressionError(ex, ex.Message);
            return Result<byte[], SerializationError>.Failure(
                new SerializationError($"GZip decompression failed: {ex.Message}") { Operation = "Decompress" }, ex);
        }
    }

    /// <inheritdoc />
    public IDictionary<string, object> GetCompressionInfo()
    {
        return new Dictionary<string, object>
(StringComparer.Ordinal)
        {
            ["Algorithm"] = "GZip",
            ["CompressionLevel"] = _compressionLevel.ToString(),
            ["BufferSize"] = _bufferSize,
            ["IsThreadSafe"] = true
        };
    }

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Information,
        Message = "GZipCompressor initialized with CompressionLevel: {CompressionLevel}, BufferSize: {BufferSize}")]
    private partial void LogCompressorInitialized(string compressionLevel, int bufferSize);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting compression of data with length {DataLength} bytes.")]
    private partial void LogCompressionStarting(int dataLength);

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "Compression completed. Data compressed from {OriginalSize} to {CompressedSize} bytes. Ratio: {Ratio:P2}")]
    private partial void LogCompressionCompleted(int originalSize, int compressedSize, float ratio);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Starting decompression of data with length {CompressedDataLength} bytes.")]
    private partial void LogDecompressionStarting(int compressedDataLength);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Decompression completed. Data decompressed from {CompressedSize} to {DecompressedSize} bytes.")]
    private partial void LogDecompressionCompleted(int compressedSize, int decompressedSize);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred while compressing data: {Message}")]
    private partial void LogCompressionError(Exception ex, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred while decompressing data: {Message}")]
    private partial void LogDecompressionError(Exception ex, string message);

    #endregion
}
