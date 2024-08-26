#region

using System.IO.Compression;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Compression;

/// <summary>
///     Provides methods to compress and decompress data using the GZip algorithm.
/// </summary>
public class GZipCompressor : ICompressor
{
    private readonly ILogger _logger;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GZipCompressor" /> class.
    /// </summary>
    public GZipCompressor()
    {
        _memoryStreamManager = new RecyclableMemoryStreamManager();
        _logger = LoggerFactory.Logger.ForContext<GZipCompressor>();
    }

    /// <inheritdoc />
    public async Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data == null)
        {
            _logger.Error("Attempted to compress null data.");
            throw new ArgumentNullException(nameof(data), "Input data cannot be null.");
        }

        _logger.Information("Starting compression of data with length {DataLength}.", data.Length);

        await using var compressedStream = _memoryStreamManager.GetStream("GZipCompressor-Compress");
        try
        {
            await using var zipStream = new GZipStream(compressedStream, CompressionMode.Compress, false);
            await zipStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            await zipStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred while compressing data.");
            throw;
        }

        compressedStream.Position = 0; // Reset position to read the stream content
        var result = compressedStream.ToArray();
        _logger.Information("Compression completed. Compressed data length: {CompressedLength}.", result.Length);

        return result;
    }

    /// <inheritdoc />
    public async Task<byte[]> DecompressAsync(byte[] compressedData, CancellationToken cancellationToken = default)
    {
        if (compressedData == null)
        {
            _logger.Error("Attempted to decompress null data.");
            throw new ArgumentNullException(nameof(compressedData), "Compressed data cannot be null.");
        }

        _logger.Information("Starting decompression of data with length {CompressedDataLength}.",
            compressedData.Length);

        await using var compressedStream =
            _memoryStreamManager.GetStream("GZipCompressor-Decompress-Input", compressedData);
        await using var decompressedStream = _memoryStreamManager.GetStream("GZipCompressor-Decompress-Output");

        try
        {
            await using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            await zipStream.CopyToAsync(decompressedStream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred while decompressing data.");
            throw;
        }

        decompressedStream.Position = 0; // Reset position to read the stream content
        var result = decompressedStream.ToArray();
        _logger.Information("Decompression completed. Decompressed data length: {DecompressedLength}.", result.Length);

        return result;
    }
}
