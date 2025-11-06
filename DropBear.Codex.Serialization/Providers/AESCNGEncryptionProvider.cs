#region

using System.Runtime.Versioning;
using System.Security.Cryptography;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Encryption;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides AES encryption services using CNG (Cryptography Next Generation).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AESCNGEncryptionProvider : IEncryptionProvider, IDisposable
{
    private readonly bool _enableCaching;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<AESCNGEncryptionProvider>();
    private readonly int _maxCacheSize;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;
    private readonly RSA _rsa;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AESCNGEncryptionProvider" /> class.
    /// </summary>
    /// <param name="config">The serialization configuration.</param>
    /// <exception cref="ArgumentException">Thrown if required configuration properties are missing.</exception>
    [SupportedOSPlatform("windows")]
    public AESCNGEncryptionProvider(SerializationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));

        _logger.Information("Initializing AESCNGEncryptionProvider.");

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

            _memoryStreamManager = config.RecyclableMemoryStreamManager ?? new RecyclableMemoryStreamManager();
            _enableCaching = config.EnableCaching;
            _maxCacheSize = config.MaxCacheSize;

            _logger.Information("AESCNGEncryptionProvider initialized successfully with KeySize: {KeySize}, " +
                                "Caching: {EnableCaching}, PublicKeyPath: {PublicKeyPath}",
                _rsa.KeySize, _enableCaching, config.PublicKeyPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize AESCNGEncryptionProvider: {Message}", ex.Message);
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

        _logger.Information("Disposing AESCNGEncryptionProvider resources.");
        _rsa.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Gets an AES encryptor using CNG implementation.
    /// </summary>
    /// <returns>An AES encryptor.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the provider has been disposed.</exception>
    [SupportedOSPlatform("windows")]
    public IEncryptor GetEncryptor()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AESCNGEncryptionProvider));
        }

        _logger.Information("Creating AES encryptor using AESCNG with KeySize: {KeySize}, Caching: {EnableCaching}",
            _rsa.KeySize, _enableCaching);

        return new AESCNGEncryptor(_rsa, _memoryStreamManager, _enableCaching, _maxCacheSize);
    }

    /// <summary>
    ///     Gets information about the encryption provider.
    /// </summary>
    /// <returns>A dictionary of information about the encryption provider.</returns>
    public IDictionary<string, object> GetProviderInfo()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Algorithm"] = "AES-CNG",
            ["KeySize"] = _rsa.KeySize,
            ["CachingEnabled"] = _enableCaching,
            ["MaxCacheSize"] = _maxCacheSize,
            ["IsThreadSafe"] = true
        };
    }
}
