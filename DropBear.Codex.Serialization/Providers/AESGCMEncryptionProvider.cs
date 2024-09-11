#region

using System.Runtime.Versioning;
using System.Security.Cryptography;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Encryption;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides RSA encryption services using AES-GCM.
/// </summary>
[SupportedOSPlatform("windows")]
public class AESGCMEncryptionProvider : IEncryptionProvider, IDisposable
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<AESGCMEncryptionProvider>();
    private readonly RSA _rsa;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AESGCMEncryptionProvider" /> class with the specified paths to public
    ///     and private keys.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public AESGCMEncryptionProvider(SerializationConfig config)
    {
        _logger.Information("Initializing AESGCMEncryptionProvider.");
        if (string.IsNullOrEmpty(config.PublicKeyPath) || string.IsNullOrEmpty(config.PrivateKeyPath))
        {
            var message = "PublicKeyPath and PrivateKeyPath must be provided in the configuration.";
            _logger.Error(message);
            throw new ArgumentException(message, nameof(config));
        }

        var rsaKeyProvider = new RSAKeyProvider(config.PublicKeyPath, config.PrivateKeyPath);
        _rsa = rsaKeyProvider.GetRsaProvider() ?? throw new InvalidOperationException("Failed to create RSA provider.");
        _logger.Information("AESGCMEncryptionProvider initialized successfully.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _logger.Information("Disposing AESGCMEncryptionProvider resources.");
        _rsa.Dispose();
    }

    /// <summary>
    ///     Gets an AES-GCM encryptor using RSA encryption.
    /// </summary>
    /// <returns>An AES-GCM encryptor using RSA encryption.</returns>
    public IEncryptor GetEncryptor()
    {
        _logger.Information("Creating AES-GCM encryptor.");
        return new AesGcmEncryptor(_rsa);
    }
}
