#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Encoders;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides Base64 encoding services.
/// </summary>
public class Base64EncodingProvider : IEncodingProvider
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<Base64EncodingProvider>();

    /// <summary>
    ///     Gets a Base64 encoder.
    /// </summary>
    /// <returns>A Base64 encoder.</returns>
    public IEncoder GetEncoder()
    {
        _logger.Information("Creating a new instance of Base64Encoder.");
        return new Base64Encoder();
    }
}
