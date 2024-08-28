#region

using System.IO.Compression;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Exceptions;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Compression;

/// <summary>
///     Provides methods to compress and decompress data using the Deflate algorithm.
/// </summary>
public class DeflateCompressor : ICompressor
{
    private readonly ILogger _logger;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DeflateCompressor" /> class.
    /// </summary>
    public DeflateCompressor()
    {
        _memoryStreamManager = new RecyclableMemoryStreamManager();
        _logger = LoggerFactory.Logger.ForContext<DeflateCompressor>();
    }

    /// <inheritdoc />
    public async Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data is null)
        {
            _logger.Error("Attempted to compress null data.");
            throw new ArgumentNullException(nameof(data), "Input data cannot be null.");
        }

        _logger.Information("Starting compression of data with length {DataLength}.", data.Length);

        var compressedStream = _memoryStreamManager.GetStream("DeflateCompressor-Compress");
        await using (compressedStream.ConfigureAwait(false))
        {
            try
            {
                await using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Compress, true);
                await deflateStream.WriteAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while compressing data.");
                throw new CompressionException("Error occurred while compressing data.", ex);
            }

            compressedStream.Position = 0; // Reset position to read the stream content
            var result = compressedStream.ToArray();
            _logger.Information("Compression completed. Compressed data length: {CompressedLength}.", result.Length);

            return result;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> DecompressAsync(byte[] compressedData, CancellationToken cancellationToken = default)
    {
        if (compressedData is null)
        {
            _logger.Error("Attempted to decompress null data.");
            throw new ArgumentNullException(nameof(compressedData), "Compressed data cannot be null.");
        }

        _logger.Information("Starting decompression of data with length {CompressedDataLength}.",
            compressedData.Length);

        var compressedStream =
            _memoryStreamManager.GetStream("DeflateCompressor-Decompress-Input", compressedData);
        await using (compressedStream.ConfigureAwait(false))
        {
            await using var decompressedStream = _memoryStreamManager.GetStream("DeflateCompressor-Decompress-Output");

            try
            {
                await using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
                await deflateStream.CopyToAsync(decompressedStream, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while decompressing data.");
                throw new CompressionException("Error occurred while decompressing data.", ex);
            }

            decompressedStream.Position = 0; // Reset position to read the stream content
            var result = decompressedStream.ToArray();
            _logger.Information("Decompression completed. Decompressed data length: {DecompressedLength}.",
                result.Length);

            return result;
        }
    }
}
