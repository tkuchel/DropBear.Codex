#region

using System.IO.Compression;
using DropBear.Codex.Serialization.Compression;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides Deflate compression services.
/// </summary>
public sealed partial class DeflateCompressionProvider : ICompressionProvider
{
    private readonly int _bufferSize;
    private readonly CompressionLevel _compressionLevel;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DeflateCompressionProvider> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DeflateCompressionProvider" /> class with default settings.
    /// </summary>
    public DeflateCompressionProvider(ILoggerFactory loggerFactory)
        : this(CompressionLevel.Fastest, 81920, loggerFactory) // Default to fastest compression and 80KB buffer
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DeflateCompressionProvider" /> class with specified settings.
    /// </summary>
    /// <param name="compressionLevel">The compression level to use.</param>
    /// <param name="bufferSize">The buffer size for compression operations.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public DeflateCompressionProvider(CompressionLevel compressionLevel, int bufferSize, ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<DeflateCompressionProvider>();
        _compressionLevel = compressionLevel;
        _bufferSize = bufferSize > 0 ? bufferSize : 81920;

        LogProviderInitialized(compressionLevel.ToString(), _bufferSize);
    }

    /// <summary>
    ///     Gets a Deflate compressor.
    /// </summary>
    /// <returns>A Deflate compressor.</returns>
    public ICompressor GetCompressor()
    {
        LogCreatingCompressor(_compressionLevel.ToString());
        return new DeflateCompressor(_compressionLevel, _bufferSize,
            _loggerFactory.CreateLogger<DeflateCompressor>());
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

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "DeflateCompressionProvider initialized with CompressionLevel: {CompressionLevel}, BufferSize: {BufferSize}")]
    private partial void LogProviderInitialized(string compressionLevel, int bufferSize);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Creating a new instance of DeflateCompressor with CompressionLevel: {CompressionLevel}")]
    private partial void LogCreatingCompressor(string compressionLevel);

    #endregion
}
