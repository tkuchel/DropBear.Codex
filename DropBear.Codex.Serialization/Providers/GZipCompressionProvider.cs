#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Compression;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides GZip compression services.
/// </summary>
public class GZipCompressionProvider : ICompressionProvider
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<GZipCompressionProvider>();

    /// <summary>
    ///     Gets a GZip compressor.
    /// </summary>
    /// <returns>A GZip compressor.</returns>
    public ICompressor GetCompressor()
    {
        _logger.Information("Creating a new instance of GZipCompressor.");
        return new GZipCompressor();
    }
}
