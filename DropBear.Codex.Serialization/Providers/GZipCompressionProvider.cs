#region

using System.IO.Compression;
using DropBear.Codex.Serialization.Compression;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides GZip compression services.
/// </summary>
public sealed partial class GZipCompressionProvider : ICompressionProvider
{
    private readonly int _bufferSize;
    private readonly CompressionLevel _compressionLevel;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GZipCompressionProvider> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GZipCompressionProvider" /> class with default settings.
    /// </summary>
    public GZipCompressionProvider(ILoggerFactory loggerFactory)
        : this(CompressionLevel.Fastest, 81920, loggerFactory) // Default to fastest compression and 80KB buffer
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="GZipCompressionProvider" /> class with specified settings.
    /// </summary>
    /// <param name="compressionLevel">The compression level to use.</param>
    /// <param name="bufferSize">The buffer size for compression operations.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public GZipCompressionProvider(CompressionLevel compressionLevel, int bufferSize, ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GZipCompressionProvider>();
        _compressionLevel = compressionLevel;
        _bufferSize = bufferSize > 0 ? bufferSize : 81920;

        LogProviderInitialized(compressionLevel.ToString(), _bufferSize);
    }

    /// <summary>
    ///     Gets a GZip compressor.
    /// </summary>
    /// <returns>A GZip compressor.</returns>
    public ICompressor GetCompressor()
    {
        LogCreatingCompressor(_compressionLevel.ToString());
        return new GZipCompressor(_compressionLevel, _bufferSize, _loggerFactory.CreateLogger<GZipCompressor>());
    }

    /// <summary>
    ///     Gets information about the compression provider.
    /// </summary>
    /// <returns>A dictionary of information about the compression provider.</returns>
    public IDictionary<string, object> GetProviderInfo()
    {
        return new Dictionary<string, object>
        {
            ["Algorithm"] = "GZip",
            ["CompressionLevel"] = _compressionLevel.ToString(),
            ["BufferSize"] = _bufferSize,
            ["IsThreadSafe"] = true
        };
    }

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Information,
        Message =
            "GZipCompressionProvider initialized with CompressionLevel: {CompressionLevel}, BufferSize: {BufferSize}")]
    private partial void LogProviderInitialized(string compressionLevel, int bufferSize);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Creating a new instance of GZipCompressor with CompressionLevel: {CompressionLevel}")]
    private partial void LogCreatingCompressor(string compressionLevel);

    #endregion
}
