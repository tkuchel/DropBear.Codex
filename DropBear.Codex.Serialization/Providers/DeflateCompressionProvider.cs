#region

using System.IO.Compression;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Compression;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides Deflate compression services.
/// </summary>
public sealed class DeflateCompressionProvider : ICompressionProvider
{
    private readonly int _bufferSize;
    private readonly CompressionLevel _compressionLevel;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<DeflateCompressionProvider>();

    /// <summary>
    ///     Initializes a new instance of the <see cref="DeflateCompressionProvider" /> class with default settings.
    /// </summary>
    public DeflateCompressionProvider()
        : this(CompressionLevel.Fastest, 81920) // Default to fastest compression and 80KB buffer
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DeflateCompressionProvider" /> class with specified settings.
    /// </summary>
    /// <param name="compressionLevel">The compression level to use.</param>
    /// <param name="bufferSize">The buffer size for compression operations.</param>
    public DeflateCompressionProvider(CompressionLevel compressionLevel, int bufferSize)
    {
        _compressionLevel = compressionLevel;
        _bufferSize = bufferSize > 0 ? bufferSize : 81920;

        _logger.Information(
            "DeflateCompressionProvider initialized with CompressionLevel: {CompressionLevel}, BufferSize: {BufferSize}",
            _compressionLevel, _bufferSize);
    }

    /// <summary>
    ///     Gets a Deflate compressor.
    /// </summary>
    /// <returns>A Deflate compressor.</returns>
    public ICompressor GetCompressor()
    {
        _logger.Information("Creating a new instance of DeflateCompressor with CompressionLevel: {CompressionLevel}",
            _compressionLevel);
        return new DeflateCompressor(_compressionLevel, _bufferSize);
    }

    /// <summary>
    ///     Gets information about the compression provider.
    /// </summary>
    /// <returns>A dictionary of information about the compression provider.</returns>
    public IDictionary<string, object> GetProviderInfo()
    {
        return new Dictionary<string, object>
        {
            ["Algorithm"] = "Deflate",
            ["CompressionLevel"] = _compressionLevel.ToString(),
            ["BufferSize"] = _bufferSize,
            ["IsThreadSafe"] = true
        };
    }
}
