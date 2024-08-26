#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Encoders;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides hexadecimal encoding services.
/// </summary>
public class HexEncodingProvider : IEncodingProvider
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<HexEncodingProvider>();

    /// <summary>
    ///     Gets a hexadecimal encoder.
    /// </summary>
    /// <returns>A hexadecimal encoder.</returns>
    public IEncoder GetEncoder()
    {
        _logger.Information("Creating a new instance of HexEncoder.");
        return new HexEncoder();
    }
}
