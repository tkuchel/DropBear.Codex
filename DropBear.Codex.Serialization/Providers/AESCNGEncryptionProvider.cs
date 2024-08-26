#region

using System.Runtime.Versioning;
using System.Security.Cryptography;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Configurations;
using DropBear.Codex.Serialization.Encryption;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides AES encryption services.
/// </summary>
[SupportedOSPlatform("windows")]
public class AESCNGEncryptionProvider : IEncryptionProvider, IDisposable
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<AESCNGEncryptionProvider>();
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;
    private readonly RSA _rsa;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AESCNGEncryptionProvider" /> class.
    /// </summary>
    /// <param name="config">The serialization configuration.</param>
    [SupportedOSPlatform("windows")]
    public AESCNGEncryptionProvider(SerializationConfig config)
    {
        _logger.Information("Initializing AESCNGEncryptionProvider.");
        if (string.IsNullOrEmpty(config.PublicKeyPath) || string.IsNullOrEmpty(config.PrivateKeyPath))
        {
            var message = "PublicKeyPath and PrivateKeyPath must be provided in the configuration.";
            _logger.Error(message);
            throw new ArgumentException(message);
        }

        var rsaKeyProvider = new RSAKeyProvider(config.PublicKeyPath, config.PrivateKeyPath);
        _rsa = rsaKeyProvider.GetRsaProvider() ?? throw new InvalidOperationException("Failed to create RSA provider.");

        _memoryStreamManager = config.RecyclableMemoryStreamManager ?? new RecyclableMemoryStreamManager();
        _logger.Information("AESCNGEncryptionProvider initialized successfully.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _logger.Information("Disposing AESCNGEncryptionProvider resources.");
        _rsa?.Dispose();
    }

    /// <summary>
    ///     Gets an AES encryptor.
    /// </summary>
    /// <returns>An AES encryptor.</returns>
    [SupportedOSPlatform("windows")]
    public IEncryptor GetEncryptor()
    {
        _logger.Information("Creating AES encryptor using AESCNG.");
        return new AESCNGEncryptor(_rsa, _memoryStreamManager);
    }
}
