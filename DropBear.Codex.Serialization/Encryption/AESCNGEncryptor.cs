#region

using System.Buffers;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Encryption;

/// <summary>
///     Provides methods to encrypt and decrypt data using AES encryption with CNG (Cryptography Next Generation).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AESCNGEncryptor : IEncryptor, IDisposable
{
    private readonly AesCng _aesCng;
    private readonly bool _enableCaching;

    // Cache for encrypted/decrypted results
    private readonly Dictionary<int, byte[]> _encryptionCache;
    private readonly ILogger _logger;
    private readonly int _maxCacheSize;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;
    private readonly RSA _rsa;

    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AESCNGEncryptor" /> class with the specified RSA key pair.
    /// </summary>
    /// <param name="rsa">The RSA key pair used for encryption.</param>
    /// <param name="memoryStreamManager">The RecyclableMemoryStreamManager instance.</param>
    /// <param name="enableCaching">Whether to enable caching of encryption results.</param>
    /// <param name="maxCacheSize">The maximum number of encryption results to cache.</param>
    [SupportedOSPlatform("windows")]
    public AESCNGEncryptor(
        RSA rsa,
        RecyclableMemoryStreamManager memoryStreamManager,
        bool enableCaching = false,
        int maxCacheSize = 100)
    {
        _rsa = rsa ?? throw new ArgumentNullException(nameof(rsa), "RSA key pair cannot be null.");
        _memoryStreamManager = memoryStreamManager ??
                               throw new ArgumentNullException(nameof(memoryStreamManager),
                                   "Memory stream manager cannot be null.");

        _aesCng = new AesCng
        {
            KeySize = 256, // Use AES-256
            BlockSize = 128, // AES block size is always 128 bits
            Mode = CipherMode.CBC,
            Padding = PaddingMode.PKCS7
        };

        _enableCaching = enableCaching;
        _maxCacheSize = maxCacheSize > 0 ? maxCacheSize : 100;

        if (_enableCaching)
        {
            _encryptionCache = new Dictionary<int, byte[]>(_maxCacheSize);
        }

        _logger = LoggerFactory.Logger.ForContext<AESCNGEncryptor>();
        _logger.Information(
            "AESCNGEncryptor initialized with CNG implementation, KeySize: {KeySize}, Caching: {EnableCaching}.",
            _aesCng.KeySize, _enableCaching);

        InitializeCryptoComponents();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.Information("Disposing AESCNGEncryptor resources.");

        _aesCng.Dispose();
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
                return Result<byte[], SerializationError>.Success([]);
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

            _logger.Information("Starting AES encryption of data with length {Length} bytes.", data.Length);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Encrypt the AES key and IV using RSA
                var encryptedKey = _rsa.Encrypt(_aesCng.Key, RSAEncryptionPadding.OaepSHA256);
                var encryptedIV = _rsa.Encrypt(_aesCng.IV, RSAEncryptionPadding.OaepSHA256);

                // Encrypt the data using AES
                using var resultStream = _memoryStreamManager.GetStream("AesEncryptor-Encrypt");

                using (var encryptor = _aesCng.CreateEncryptor())
                using (var cryptoStream = new CryptoStream(resultStream, encryptor, CryptoStreamMode.Write, true))
                {
                    await cryptoStream.WriteAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
                    await cryptoStream.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);
                } // cryptoStream and encryptor are disposed here

                // Combine all components into a single result
                var encryptedData = resultStream.ToArray();
                var combinedResult = CombineEncryptedComponents(encryptedKey, encryptedIV, encryptedData);

                stopwatch.Stop();
                _logger.Information("AES encryption completed successfully in {ElapsedMs}ms. " +
                                    "Original size: {OriginalSize}, Encrypted size: {EncryptedSize}",
                    stopwatch.ElapsedMilliseconds, data.Length, combinedResult.Length);

                // Cache the result if enabled
                if (_enableCaching)
                {
                    CacheEncryptionResult(data, combinedResult);
                }

                return Result<byte[], SerializationError>.Success(combinedResult);
            }
            catch (CryptographicException ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "Cryptographic error during AES encryption: {Message}", ex.Message);
                return Result<byte[], SerializationError>.Failure(
                    new SerializationError($"AES encryption failed: {ex.Message}") { Operation = "Encrypt" }, ex);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during AES encryption: {Message}", ex.Message);
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
                return Result<byte[], SerializationError>.Success([]);
            }

            _logger.Information("Starting AES decryption of data with length {Length} bytes.", data.Length);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Extract encrypted key, IV, and data
                var keySizeBytes = _rsa.KeySize / 8; // Calculate based on RSA key size

                if (data.Length <= 2 * keySizeBytes)
                {
                    throw new CryptographicException("Encrypted data is too short to contain required components");
                }

                var encryptedKey = data.AsSpan(0, keySizeBytes).ToArray();
                var encryptedIV = data.AsSpan(keySizeBytes, keySizeBytes).ToArray();
                var encryptedData = data.AsSpan(2 * keySizeBytes).ToArray();

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
                    _logger.Error(ex, "Failed to decrypt AES key or IV: {Message}", ex.Message);
                    return Result<byte[], SerializationError>.Failure(
                        new SerializationError("RSA decryption of AES key or IV failed") { Operation = "Decrypt" }, ex);
                }

                // Decrypt data
                using var resultStream = _memoryStreamManager.GetStream("AesEncryptor-Decrypt");
                using var inputStream = _memoryStreamManager.GetStream("AesEncryptor-Decrypt-Input", encryptedData);

                using (var decryptor = _aesCng.CreateDecryptor(aesKey, aesIV))
                using (var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read))
                {
                    // Use shared buffer from array pool
                    var buffer = ArrayPool<byte>.Shared.Rent(81920); // 80KB buffer

                    try
                    {
                        int bytesRead;
                        while ((bytesRead = await cryptoStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                                   .ConfigureAwait(false)) > 0)
                        {
                            await resultStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                // Get the decrypted data
                resultStream.Position = 0;
                var plaintext = resultStream.ToArray();

                stopwatch.Stop();
                _logger.Information("AES decryption completed successfully in {ElapsedMs}ms. " +
                                    "Encrypted size: {EncryptedSize}, Decrypted size: {DecryptedSize}",
                    stopwatch.ElapsedMilliseconds, data.Length, plaintext.Length);

                return Result<byte[], SerializationError>.Success(plaintext);
            }
            catch (CryptographicException ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "Cryptographic error during AES decryption: {Message}", ex.Message);
                return Result<byte[], SerializationError>.Failure(
                    new SerializationError($"AES decryption failed: {ex.Message}") { Operation = "Decrypt" }, ex);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during AES decryption: {Message}", ex.Message);
            return Result<byte[], SerializationError>.Failure(
                new SerializationError($"Decryption error: {ex.Message}") { Operation = "Decrypt" }, ex);
        }
    }

    /// <summary>
    ///     Initializes the cryptographic components with secure random values.
    /// </summary>
    private void InitializeCryptoComponents()
    {
        using var rng = RandomNumberGenerator.Create();
        _aesCng.Key = new byte[32]; // 256 bits for AES-256
        _aesCng.IV = new byte[16]; // 128 bits for AES block size
        rng.GetBytes(_aesCng.Key);
        rng.GetBytes(_aesCng.IV);
    }

    /// <summary>
    ///     Combines multiple byte arrays into a single array.
    /// </summary>
    private static byte[] CombineEncryptedComponents(byte[] encryptedKey, byte[] encryptedIV, byte[] encryptedData)
    {
        var totalLength = encryptedKey.Length + encryptedIV.Length + encryptedData.Length;
        var result = new byte[totalLength];

        var position = 0;

        Buffer.BlockCopy(encryptedKey, 0, result, position, encryptedKey.Length);
        position += encryptedKey.Length;

        Buffer.BlockCopy(encryptedIV, 0, result, position, encryptedIV.Length);
        position += encryptedIV.Length;

        Buffer.BlockCopy(encryptedData, 0, result, position, encryptedData.Length);

        return result;
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
            throw new ObjectDisposedException(nameof(AESCNGEncryptor));
        }
    }
}
