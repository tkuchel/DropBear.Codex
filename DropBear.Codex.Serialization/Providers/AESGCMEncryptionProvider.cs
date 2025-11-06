#region

using System.Runtime.Versioning;
using System.Security.Cryptography;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Encryption;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.Extensions.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides RSA encryption services using AES-GCM.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AESGCMEncryptionProvider : IEncryptionProvider, IDisposable
{
    private readonly bool _enableCaching;
    private readonly Serilog.ILogger _logger = DropBear.Codex.Core.Logging.LoggerFactory.Logger.ForContext<AESGCMEncryptionProvider>();
    private readonly ILoggerFactory _loggerFactory;
    private readonly int _maxCacheSize;
    private readonly RSA _rsa;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AESGCMEncryptionProvider" /> class.
    /// </summary>
    /// <param name="config">The serialization configuration.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <exception cref="ArgumentException">Thrown if required configuration properties are missing.</exception>
    [SupportedOSPlatform("windows")]
    public AESGCMEncryptionProvider(SerializationConfig config, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        _logger.Information("Initializing AESGCMEncryptionProvider.");

        // Validate key paths
        if (string.IsNullOrEmpty(config.PublicKeyPath) || string.IsNullOrEmpty(config.PrivateKeyPath))
        {
            var message = "PublicKeyPath and PrivateKeyPath must be provided in the configuration.";
            _logger.Error(message);
            throw new ArgumentException(message, nameof(config));
        }

        try
        {
            var rsaKeyProvider = new RSAKeyProvider(config.PublicKeyPath, config.PrivateKeyPath);
            _rsa = rsaKeyProvider.GetRsaProvider() ??
                   throw new InvalidOperationException("Failed to create RSA provider.");

            _enableCaching = config.EnableCaching;
            _maxCacheSize = config.MaxCacheSize;

            _logger.Information("AESGCMEncryptionProvider initialized successfully with KeySize: {KeySize}, " +
                                "Caching: {EnableCaching}, PublicKeyPath: {PublicKeyPath}",
                _rsa.KeySize, _enableCaching, config.PublicKeyPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize AESGCMEncryptionProvider: {Message}", ex.Message);
            throw new InvalidOperationException("Failed to initialize encryption provider.", ex);
        }
    }

    /// <summary>
    ///     Disposes the encryption provider and releases RSA resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.Information("Disposing AESGCMEncryptionProvider resources.");
        _rsa.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Gets an AES-GCM encryptor using RSA encryption.
    /// </summary>
    /// <returns>An AES-GCM encryptor.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the provider has been disposed.</exception>
    public IEncryptor GetEncryptor()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AESGCMEncryptionProvider));
        }

        _logger.Information("Creating AES-GCM encryptor with KeySize: {KeySize}, Caching: {EnableCaching}",
            _rsa.KeySize, _enableCaching);

        var logger = _loggerFactory.CreateLogger<AesGcmEncryptor>();
        return new AesGcmEncryptor(_rsa, logger, _enableCaching, _maxCacheSize);
    }

    /// <summary>
    ///     Gets information about the encryption provider.
    /// </summary>
    /// <returns>A dictionary of information about the encryption provider.</returns>
    public IDictionary<string, object> GetProviderInfo()
    {
        return new Dictionary<string, object>
(StringComparer.Ordinal)
        {
            ["Algorithm"] = "AES-GCM",
            ["KeySize"] = _rsa.KeySize,
            ["CachingEnabled"] = _enableCaching,
            ["MaxCacheSize"] = _maxCacheSize,
            ["IsThreadSafe"] = true
        };
    }
}
