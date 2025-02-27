#region

using System.Diagnostics;
using System.Security.Cryptography;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Encryption;

/// <summary>
///     Provides methods to encrypt and decrypt data using AES-GCM encryption.
/// </summary>
public sealed class AesGcmEncryptor : IEncryptor, IDisposable
{
    private const int KeySize = 32; // AES-256 key size in bytes
    private const int TagSize = 16; // GCM tag size in bytes
    private const int NonceSize = 12; // Recommended for GCM
    private readonly bool _enableCaching;

    // Cache for encrypted/decrypted results
    private readonly Dictionary<int, byte[]> _encryptionCache;

    private readonly byte[] _key;

    private readonly ILogger _logger;
    private readonly int _maxCacheSize;
    private readonly RSA _rsa;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AesGcmEncryptor" /> class with the specified RSA key pair.
    /// </summary>
    /// <param name="rsa">The RSA key pair used for encryption.</param>
    /// <param name="enableCaching">Whether to enable caching of encryption results.</param>
    /// <param name="maxCacheSize">The maximum number of encryption results to cache.</param>
    public AesGcmEncryptor(RSA rsa, bool enableCaching = false, int maxCacheSize = 100)
    {
        _rsa = rsa ?? throw new ArgumentNullException(nameof(rsa), "RSA key pair cannot be null.");
        _key = GenerateKey();
        _enableCaching = enableCaching;
        _maxCacheSize = maxCacheSize > 0 ? maxCacheSize : 100;

        if (_enableCaching)
        {
            _encryptionCache = new Dictionary<int, byte[]>(_maxCacheSize);
        }

        _logger = LoggerFactory.Logger.ForContext<AesGcmEncryptor>();
        _logger.Information("AesGcmEncryptor initialized with caching: {EnableCaching}, MaxCacheSize: {MaxCacheSize}.",
            _enableCaching, _maxCacheSize);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.Information("Disposing AesGcmEncryptor and securely erasing the key.");

        // Securely erase the key
        if (_key.Length > 0)
        {
            Array.Clear(_key, 0, _key.Length);
        }

        // Dispose of RSA resources
        _rsa.Dispose();

        // Clear the cache if it exists
        if (_enableCaching && _encryptionCache != null)
        {
            _encryptionCache.Clear();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async Task<Result<byte[], SerializationError>> EncryptAsync(byte[] data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate input
            ArgumentNullException.ThrowIfNull(data, nameof(data));
            ThrowIfDisposed();

            if (data.Length == 0)
            {
                return Result<byte[], SerializationError>.Success(Array.Empty<byte>());
            }

            // Try cache lookup if enabled
            if (_enableCaching)
            {
                var dataHash = CalculateHash(data);

                if (_encryptionCache.TryGetValue(dataHash, out var cachedResult))
                {
                    _logger.Information("Cache hit for encryption, returning cached result.");
                    return Result<byte[], SerializationError>.Success(cachedResult);
                }
            }

            _logger.Information("Starting encryption of data with length {Length} bytes.", data.Length);
            var stopwatch = Stopwatch.StartNew();

            // Generate random nonce
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            // Prepare ciphertext and tag buffers
            var ciphertext = new byte[data.Length];
            var tag = new byte[TagSize];

            try
            {
                // Perform encryption
                using var aesGcm = new AesGcm(_key, TagSize);
                aesGcm.Encrypt(nonce, data, ciphertext, tag);

                // Encrypt the key and nonce with RSA
                var encryptedKey = await EncryptWithRsaAsync(_key, cancellationToken).ConfigureAwait(false);
                var encryptedNonce = await EncryptWithRsaAsync(nonce, cancellationToken).ConfigureAwait(false);

                // Combine everything into a single result
                var combinedData = CombineEncryptedComponents(encryptedKey, encryptedNonce, tag, ciphertext);

                stopwatch.Stop();
                _logger.Information("Encryption completed successfully in {ElapsedMs}ms. " +
                                    "Original size: {OriginalSize}, Encrypted size: {EncryptedSize}",
                    stopwatch.ElapsedMilliseconds, data.Length, combinedData.Length);

                // Cache the result if enabled
                if (_enableCaching)
                {
                    CacheEncryptionResult(data, combinedData);
                }

                return Result<byte[], SerializationError>.Success(combinedData);
            }
            catch (CryptographicException ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "Cryptographic error during encryption: {Message}", ex.Message);
                return Result<byte[], SerializationError>.Failure(
                    new SerializationError($"AES-GCM encryption failed: {ex.Message}") { Operation = "Encrypt" }, ex);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during encryption: {Message}", ex.Message);
            return Result<byte[], SerializationError>.Failure(
                new SerializationError($"Encryption error: {ex.Message}") { Operation = "Encrypt" }, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<byte[], SerializationError>> DecryptAsync(byte[] data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate input
            ArgumentNullException.ThrowIfNull(data, nameof(data));
            ThrowIfDisposed();

            if (data.Length == 0)
            {
                return Result<byte[], SerializationError>.Success(Array.Empty<byte>());
            }

            _logger.Information("Starting decryption of data with length {Length} bytes.", data.Length);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Extract key, nonce, tag, and ciphertext from combined data
                var keySizeBytes = _rsa.KeySize / 8;

                if (data.Length <= (2 * keySizeBytes) + TagSize)
                {
                    throw new CryptographicException("Encrypted data is too short to contain required components");
                }

                var encryptedKey = data.AsSpan(0, keySizeBytes).ToArray();
                var encryptedNonce = data.AsSpan(keySizeBytes, keySizeBytes).ToArray();
                var tag = data.AsSpan(2 * keySizeBytes, TagSize).ToArray();
                var ciphertext = data.AsSpan((2 * keySizeBytes) + TagSize).ToArray();

                // Decrypt key and nonce
                var key = await DecryptWithRsaAsync(encryptedKey, cancellationToken).ConfigureAwait(false);
                var nonce = await DecryptWithRsaAsync(encryptedNonce, cancellationToken).ConfigureAwait(false);

                // Allocate buffer for plaintext
                var plaintext = new byte[ciphertext.Length];

                // Decrypt the data
                using var aesGcm = new AesGcm(key, TagSize);
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

                stopwatch.Stop();
                _logger.Information("Decryption completed successfully in {ElapsedMs}ms. " +
                                    "Encrypted size: {EncryptedSize}, Decrypted size: {DecryptedSize}",
                    stopwatch.ElapsedMilliseconds, data.Length, plaintext.Length);

                return Result<byte[], SerializationError>.Success(plaintext);
            }
            catch (CryptographicException ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "Cryptographic error during decryption: {Message}", ex.Message);
                return Result<byte[], SerializationError>.Failure(
                    new SerializationError($"AES-GCM decryption failed: {ex.Message}") { Operation = "Decrypt" }, ex);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during decryption: {Message}", ex.Message);
            return Result<byte[], SerializationError>.Failure(
                new SerializationError($"Decryption error: {ex.Message}") { Operation = "Decrypt" }, ex);
        }
    }

    /// <summary>
    ///     Generates a cryptographically secure random key for AES encryption.
    /// </summary>
    /// <returns>A random key of the specified key size.</returns>
    private static byte[] GenerateKey()
    {
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    /// <summary>
    ///     Combines multiple byte arrays into a single array.
    /// </summary>
    private static byte[] CombineEncryptedComponents(byte[] encryptedKey, byte[] encryptedNonce, byte[] tag,
        byte[] ciphertext)
    {
        var totalLength = encryptedKey.Length + encryptedNonce.Length + tag.Length + ciphertext.Length;
        var result = new byte[totalLength];

        var position = 0;

        Buffer.BlockCopy(encryptedKey, 0, result, position, encryptedKey.Length);
        position += encryptedKey.Length;

        Buffer.BlockCopy(encryptedNonce, 0, result, position, encryptedNonce.Length);
        position += encryptedNonce.Length;

        Buffer.BlockCopy(tag, 0, result, position, tag.Length);
        position += tag.Length;

        Buffer.BlockCopy(ciphertext, 0, result, position, ciphertext.Length);

        return result;
    }

    /// <summary>
    ///     Encrypts data using RSA.
    /// </summary>
    private async Task<byte[]> EncryptWithRsaAsync(byte[] data, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Decrypts data using RSA.
    /// </summary>
    private async Task<byte[]> DecryptWithRsaAsync(byte[] encryptedData, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _rsa.Decrypt(encryptedData, RSAEncryptionPadding.OaepSHA256);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Calculates a hash of the given data for caching purposes.
    /// </summary>
    private int CalculateHash(byte[] data)
    {
        // For small data, use the built-in hash code
        if (data.Length <= 256)
        {
            return BitConverter.ToInt32(SHA256.HashData(data).AsSpan(0, 4));
        }

        // For larger data, compute a faster hash
        unchecked
        {
            const int p = 16777619;
            var hash = (int)2166136261;

            // Sample data at intervals for a faster but reasonably unique hash
            var step = Math.Max(1, data.Length / 64);

            for (var i = 0; i < data.Length; i += step)
            {
                hash = (hash ^ data[i]) * p;
            }

            hash += hash << 13;
            hash ^= hash >> 7;
            hash += hash << 3;
            hash ^= hash >> 17;
            hash += hash << 5;

            return hash;
        }
    }

    /// <summary>
    ///     Caches an encryption result.
    /// </summary>
    private void CacheEncryptionResult(byte[] original, byte[] encrypted)
    {
        if (!_enableCaching || _encryptionCache == null)
        {
            return;
        }

        try
        {
            var hash = CalculateHash(original);

            // If cache is full, remove oldest entry
            if (_encryptionCache.Count >= _maxCacheSize)
            {
                var keyToRemove = _encryptionCache.Keys.First();
                _encryptionCache.Remove(keyToRemove);
            }

            _encryptionCache[hash] = encrypted;
        }
        catch (Exception ex)
        {
            // Don't let caching errors affect the core functionality
            _logger.Warning(ex, "Error while caching encryption result: {Message}", ex.Message);
        }
    }

    /// <summary>
    ///     Throws an ObjectDisposedException if this object has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AesGcmEncryptor));
        }
    }
}
