#region

using System.Runtime.Versioning;
using System.Security.Cryptography;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Encryption;

/// <summary>
///     Provides methods to encrypt and decrypt data using AES encryption.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AESCNGEncryptor : IEncryptor, IDisposable
{
    private readonly AesCng _aesCng;
    private readonly ILogger _logger;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;
    private readonly RSA _rsa;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AESCNGEncryptor" /> class with the specified RSA key pair.
    /// </summary>
    /// <param name="rsa">The RSA key pair used for encryption.</param>
    /// <param name="memoryStreamManager">The RecyclableMemoryStreamManager instance.</param>
    public AESCNGEncryptor(RSA rsa, RecyclableMemoryStreamManager memoryStreamManager)
    {
        _aesCng = new AesCng();
        _rsa = rsa ?? throw new ArgumentNullException(nameof(rsa), "RSA key pair cannot be null.");
        _memoryStreamManager = memoryStreamManager ??
                               throw new ArgumentNullException(nameof(memoryStreamManager),
                                   "Memory stream manager cannot be null.");
        _logger = LoggerFactory.Logger.ForContext<AESCNGEncryptor>();
        InitializeCryptoComponents();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _aesCng.Dispose();
        // GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async Task<byte[]> EncryptAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data == null)
        {
            _logger.Error("Input data cannot be null.");
            throw new ArgumentNullException(nameof(data), "Input data cannot be null.");
        }

        try
        {
            var encryptedKey = _rsa.Encrypt(_aesCng.Key, RSAEncryptionPadding.OaepSHA256);
            var encryptedIV = _rsa.Encrypt(_aesCng.IV, RSAEncryptionPadding.OaepSHA256);

            var resultStream = _memoryStreamManager.GetStream("AesEncryptor-Encrypt");
            await using (resultStream.ConfigureAwait(false))
            {
                await using var cryptoStream =
                    new CryptoStream(resultStream, _aesCng.CreateEncryptor(), CryptoStreamMode.Write);

                await cryptoStream.WriteAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
                await cryptoStream.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);

                return Combine(encryptedKey, encryptedIV, resultStream.ToArray());
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during AES encryption.");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> DecryptAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data == null)
        {
            _logger.Error("Input data cannot be null.");
            throw new ArgumentNullException(nameof(data), "Input data cannot be null.");
        }

        try
        {
            // Extract encrypted key, IV, and data
            var keySizeBytes = _rsa.KeySize / 8; // Calculate based on RSA key size
            var encryptedKey = data.Take(keySizeBytes).ToArray();
            var encryptedIV = data.Skip(keySizeBytes).Take(keySizeBytes).ToArray();
            var encryptedData = data.Skip(2 * keySizeBytes).ToArray();

            // Decrypt key and IV
            byte[] aesKey;
            byte[] aesIV;
            try
            {
                aesKey = _rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
                aesIV = _rsa.Decrypt(encryptedIV, RSAEncryptionPadding.OaepSHA256);
            }
            catch (CryptographicException ex)
            {
                _logger.Error(ex, "Decryption failed. RSA key or padding is incorrect.");
                throw new InvalidOperationException("Decryption failed. RSA key or padding is incorrect.", ex);
            }

            // Decrypt data
            var resultStream = _memoryStreamManager.GetStream("AesEncryptor-Decrypt");

            // Decrypt data
            await using (resultStream.ConfigureAwait(false))
            {
                await using var cryptoStream = new CryptoStream(resultStream, _aesCng.CreateDecryptor(aesKey, aesIV),
                    CryptoStreamMode.Write);

                await cryptoStream.WriteAsync(encryptedData.AsMemory(), cancellationToken).ConfigureAwait(false);
                await cryptoStream.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);

                return await ReadStreamAsync(resultStream, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during AES decryption.");
            throw;
        }
    }

    private void InitializeCryptoComponents()
    {
        using var rng = RandomNumberGenerator.Create();
        _aesCng.Key = new byte[32]; // 256 bits for AES-256
        _aesCng.IV = new byte[16]; // 128 bits for AES block size
        rng.GetBytes(_aesCng.Key);
        rng.GetBytes(_aesCng.IV);
    }

    private static byte[] Combine(params byte[][] arrays)
    {
        var totalLength = arrays.Sum(array => array.Length);
        var result = new byte[totalLength];
        var offset = 0;
        foreach (var array in arrays)
        {
            Buffer.BlockCopy(array, 0, result, offset, array.Length);
            offset += array.Length;
        }

        return result;
    }

    private static async Task<byte[]> ReadStreamAsync(RecyclableMemoryStream resultStream,
        CancellationToken cancellationToken)
    {
        resultStream.Position = 0;
        using var memoryStream = new MemoryStream();
        await resultStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        return memoryStream.ToArray();
    }
}
