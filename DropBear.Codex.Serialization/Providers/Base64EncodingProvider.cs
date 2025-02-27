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
public sealed class Base64EncodingProvider : IEncodingProvider
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<Base64EncodingProvider>();
    private readonly bool _useUrlSafeEncoding;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Base64EncodingProvider" /> class.
    /// </summary>
    /// <param name="useUrlSafeEncoding">Whether to use URL-safe Base64 encoding.</param>
    public Base64EncodingProvider(bool useUrlSafeEncoding = false)
    {
        _useUrlSafeEncoding = useUrlSafeEncoding;
        _logger.Information("Base64EncodingProvider initialized with UrlSafeEncoding: {UseUrlSafeEncoding}",
            _useUrlSafeEncoding);
    }

    /// <summary>
    ///     Gets a Base64 encoder.
    /// </summary>
    /// <returns>A Base64 encoder.</returns>
    public IEncoder GetEncoder()
    {
        _logger.Information("Creating a new instance of Base64Encoder with UrlSafeEncoding: {UseUrlSafeEncoding}",
            _useUrlSafeEncoding);
        return new Base64Encoder(_useUrlSafeEncoding);
    }

    /// <summary>
    ///     Gets information about the encoding provider.
    /// </summary>
    /// <returns>A dictionary of information about the encoding provider.</returns>
    public IDictionary<string, object> GetProviderInfo()
    {
        return new Dictionary<string, object>
        {
            ["EncodingType"] = "Base64", ["UrlSafeEncoding"] = _useUrlSafeEncoding, ["IsThreadSafe"] = true
        };
    }
}
