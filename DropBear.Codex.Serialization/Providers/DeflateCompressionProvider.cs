#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Compression;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides Deflate compression services.
/// </summary>
public class DeflateCompressionProvider : ICompressionProvider
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<DeflateCompressionProvider>();

    /// <summary>
    ///     Gets a Deflate compressor.
    /// </summary>
    /// <returns>A Deflate compressor.</returns>
    public ICompressor GetCompressor()
    {
        _logger.Information("Creating a new instance of DeflateCompressor.");
        return new DeflateCompressor();
    }
}
