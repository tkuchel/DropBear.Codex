#region

using System.Security.Cryptography;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Encryption;

/// <summary>
///     Provides methods to encrypt and decrypt data using AES-GCM encryption.
/// </summary>
public class AesGcmEncryptor : IEncryptor, IDisposable
{
    private const int KeySize = 32; // AES-256 key size in bytes
    private const int TagSize = 16; // GCM tag size in bytes
    private readonly ILogger _logger;

    private readonly RSA _rsa;
    private byte[] _key;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AesGcmEncryptor" /> class with the specified RSA key pair.
    /// </summary>
    /// <param name="rsa">The RSA key pair used for encryption.</param>
    public AesGcmEncryptor(RSA rsa)
    {
        _rsa = rsa ?? throw new ArgumentNullException(nameof(rsa), "RSA key pair cannot be null.");
        _key = GenerateKey();
        _logger = LoggerFactory.Logger.ForContext<AesGcmEncryptor>();
        _logger.Information("AesGcmEncryptor initialized with a new key.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _logger.Information("Disposing AesGcmEncryptor and securely erasing the key.");
        Array.Clear(_key, 0, _key.Length); // Securely erase the key
        _rsa.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async Task<byte[]> EncryptAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        _ = data ?? throw new ArgumentNullException(nameof(data), "Input data cannot be null.");
        _logger.Information("Starting encryption of data with length {Length}.", data.Length);

        try
        {
            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            RandomNumberGenerator.Fill(nonce);

            var ciphertext = new byte[data.Length];
            var tag = new byte[TagSize];

            using var aesGcm = new AesGcm(_key);
            aesGcm.Encrypt(nonce, data, ciphertext, tag);

            var encryptedKey = await Task
                .Run(() => _rsa.Encrypt(_key, RSAEncryptionPadding.OaepSHA256), cancellationToken)
                .ConfigureAwait(false);
            var encryptedNonce = await Task
                .Run(() => _rsa.Encrypt(nonce, RSAEncryptionPadding.OaepSHA256), cancellationToken)
                .ConfigureAwait(false);

            var combinedData = Combine(encryptedKey, encryptedNonce, tag, ciphertext);
            _logger.Information("Encryption completed successfully.");

            return combinedData;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during encryption.");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> DecryptAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        _ = data ?? throw new ArgumentNullException(nameof(data), "Input encrypted data cannot be null.");
        _logger.Information("Starting decryption of data with length {Length}.", data.Length);

        try
        {
            var keySizeBytes = _rsa.KeySize / 8;
            var encryptedKey = data.Take(keySizeBytes).ToArray();
            var encryptedNonce = data.Skip(keySizeBytes).Take(keySizeBytes).ToArray();
            var tag = data.Skip(2 * keySizeBytes).Take(TagSize).ToArray();
            var ciphertext = data.Skip((2 * keySizeBytes) + tag.Length).ToArray();

            _key = await Task.Run(() => _rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256), cancellationToken)
                .ConfigureAwait(false);
            var nonce = await Task
                .Run(() => _rsa.Decrypt(encryptedNonce, RSAEncryptionPadding.OaepSHA256), cancellationToken)
                .ConfigureAwait(false);

            var plaintext = new byte[ciphertext.Length];
            using var aesGcm = new AesGcm(_key);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

            _logger.Information("Decryption completed successfully.");
            return plaintext;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during decryption.");
            throw;
        }
    }

    private static byte[] GenerateKey()
    {
        var key = new byte[KeySize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);
        return key;
    }

    private static byte[] Combine(params byte[][] arrays)
    {
        var combined = new byte[arrays.Sum(a => a.Length)];
        var offset = 0;
        foreach (var array in arrays)
        {
            Buffer.BlockCopy(array, 0, combined, offset, array.Length);
            offset += array.Length;
        }

        return combined;
    }
}
