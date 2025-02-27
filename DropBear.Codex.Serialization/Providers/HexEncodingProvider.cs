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
public sealed class HexEncodingProvider : IEncodingProvider
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<HexEncodingProvider>();
    private readonly bool _upperCase;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HexEncodingProvider" /> class.
    /// </summary>
    /// <param name="upperCase">Whether to use uppercase hexadecimal characters.</param>
    public HexEncodingProvider(bool upperCase = true)
    {
        _upperCase = upperCase;
        _logger.Information("HexEncodingProvider initialized with UpperCase: {UpperCase}", upperCase);
    }

    /// <summary>
    ///     Gets a hexadecimal encoder.
    /// </summary>
    /// <returns>A hexadecimal encoder.</returns>
    public IEncoder GetEncoder()
    {
        _logger.Information("Creating a new instance of HexEncoder with UpperCase: {UpperCase}", _upperCase);
        return new HexEncoder(_upperCase);
    }

    /// <summary>
    ///     Gets information about the encoding provider.
    /// </summary>
    /// <returns>A dictionary of information about the encoding provider.</returns>
    public IDictionary<string, object> GetProviderInfo()
    {
        return new Dictionary<string, object>
        {
            ["EncodingType"] = "Hexadecimal", ["UpperCase"] = _upperCase, ["IsThreadSafe"] = true
        };
    }
}
