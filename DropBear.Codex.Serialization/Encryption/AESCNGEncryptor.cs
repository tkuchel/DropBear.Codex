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
    private const byte FormatVersion = 1;
    private const int AuthenticationTagSize = 32;

    private readonly AesCng _aesCng;
    private readonly ILogger _logger;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;
    private readonly RSA _rsa;

    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AESCNGEncryptor" /> class with the specified RSA key pair.
    /// </summary>
    /// <param name="rsa">The RSA key pair used for encryption.</param>
    /// <param name="memoryStreamManager">The RecyclableMemoryStreamManager instance.</param>
    /// <param name="enableCaching">Deprecated parameter, no longer used. Caching is disabled for security.</param>
    /// <param name="maxCacheSize">Deprecated parameter, no longer used.</param>
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

        _logger = LoggerFactory.Logger.ForContext<AESCNGEncryptor>();
        _logger.Information(
            "AESCNGEncryptor initialized with CNG implementation, KeySize: {KeySize}. Encryption caching is disabled for security.",
            _aesCng.KeySize);

        InitializeCryptoComponents();
    }

    /// <summary>
    ///     Disposes the encryptor and releases cryptographic resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.Information("Disposing AESCNGEncryptor resources.");

        _aesCng.Dispose();
        _rsa.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Encrypts the specified data using AES encryption with CNG.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A result containing the encrypted data on success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the encryptor has been disposed.</exception>
    /// <exception cref="CryptographicException">Thrown when encryption fails.</exception>
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

            _logger.Information("Starting AES encryption of data with length {Length} bytes.", data.Length);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // SECURITY FIX: Generate a fresh IV for each encryption operation to prevent IV reuse attacks
                // IV reuse in CBC mode can leak information about repeated plaintexts
                var freshIV = new byte[16]; // 128 bits for AES block size
                RandomNumberGenerator.Fill(freshIV);

                var authenticationKey = new byte[32];
                RandomNumberGenerator.Fill(authenticationKey);

                // Encrypt the AES key, fresh IV, and authentication key using RSA
                var encryptedKey = _rsa.Encrypt(_aesCng.Key, RSAEncryptionPadding.OaepSHA256);
                var encryptedIV = _rsa.Encrypt(freshIV, RSAEncryptionPadding.OaepSHA256);
                var encryptedAuthenticationKey = _rsa.Encrypt(authenticationKey, RSAEncryptionPadding.OaepSHA256);

                // Encrypt the data using AES with the fresh IV
                using var resultStream = _memoryStreamManager.GetStream("AesEncryptor-Encrypt");

                using (var encryptor = _aesCng.CreateEncryptor(_aesCng.Key, freshIV))
                using (var cryptoStream = new CryptoStream(resultStream, encryptor, CryptoStreamMode.Write, true))
                {
                    await cryptoStream.WriteAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
                    await cryptoStream.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);
                } // cryptoStream and encryptor are disposed here

                var encryptedData = resultStream.ToArray();
                var authenticationTag = ComputeAuthenticationTag(
                    authenticationKey,
                    encryptedKey,
                    encryptedIV,
                    encryptedAuthenticationKey,
                    encryptedData);

                // Securely clear the fresh IV and authentication key from memory
                Array.Clear(freshIV, 0, freshIV.Length);
                Array.Clear(authenticationKey, 0, authenticationKey.Length);

                // Combine all components into a single result
                var combinedResult = CombineEncryptedComponents(
                    encryptedKey,
                    encryptedIV,
                    encryptedAuthenticationKey,
                    authenticationTag,
                    encryptedData);

                stopwatch.Stop();
                _logger.Information("AES encryption completed successfully in {ElapsedMs}ms. " +
                                    "Original size: {OriginalSize}, Encrypted size: {EncryptedSize}",
                    stopwatch.ElapsedMilliseconds, data.Length, combinedResult.Length);

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

    /// <summary>
    ///     Decrypts the specified AES-CNG encrypted data.
    /// </summary>
    /// <param name="data">The encrypted data to decrypt.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A result containing the decrypted data on success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the encryptor has been disposed.</exception>
    /// <exception cref="CryptographicException">Thrown when decryption fails or data is corrupted.</exception>
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
                // Extract encrypted key, IV, authentication key, tag, and data
                var keySizeBytes = _rsa.KeySize / 8; // Calculate based on RSA key size

                if (data.Length < 1 + (3 * keySizeBytes) + AuthenticationTagSize + 1)
                {
                    throw new CryptographicException("Encrypted data is too short to contain required components");
                }

                if (data[0] != FormatVersion)
                {
                    throw new CryptographicException("Unsupported or legacy unauthenticated AES-CNG payload format");
                }

                var position = 1;
                var encryptedKey = data.AsSpan(position, keySizeBytes).ToArray();
                position += keySizeBytes;
                var encryptedIV = data.AsSpan(position, keySizeBytes).ToArray();
                position += keySizeBytes;
                var encryptedAuthenticationKey = data.AsSpan(position, keySizeBytes).ToArray();
                position += keySizeBytes;
                var authenticationTag = data.AsSpan(position, AuthenticationTagSize).ToArray();
                position += AuthenticationTagSize;
                var encryptedData = data.AsSpan(position).ToArray();

                // Decrypt key and IV
                byte[]? aesKey = null;
                byte[]? aesIV = null;
                byte[]? authenticationKey = null;

                try
                {
                    aesKey = _rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
                    aesIV = _rsa.Decrypt(encryptedIV, RSAEncryptionPadding.OaepSHA256);
                    authenticationKey = _rsa.Decrypt(encryptedAuthenticationKey, RSAEncryptionPadding.OaepSHA256);
                }
                catch (CryptographicException ex)
                {
                    _logger.Error(ex, "Failed to decrypt AES key, IV, or authentication key: {Message}", ex.Message);
                    return Result<byte[], SerializationError>.Failure(
                        new SerializationError("RSA decryption of AES key, IV, or authentication key failed") { Operation = "Decrypt" }, ex);
                }

                var expectedTag = ComputeAuthenticationTag(
                    authenticationKey,
                    encryptedKey,
                    encryptedIV,
                    encryptedAuthenticationKey,
                    encryptedData);

                if (!CryptographicOperations.FixedTimeEquals(authenticationTag, expectedTag))
                {
                    Array.Clear(expectedTag, 0, expectedTag.Length);
                    return Result<byte[], SerializationError>.Failure(
                        new SerializationError("Encrypted data authentication failed") { Operation = "Decrypt" });
                }

                Array.Clear(expectedTag, 0, expectedTag.Length);

                byte[] plaintext;

                try
                {
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
                            // Clear buffer before returning to pool
                            Array.Clear(buffer, 0, buffer.Length);
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }

                    // Get the decrypted data
                    resultStream.Position = 0;
                    plaintext = resultStream.ToArray();
                }
                finally
                {
                    // SECURITY FIX: Securely clear decrypted key and IV from memory
                    // This prevents sensitive cryptographic material from lingering in memory
                    if (aesKey != null)
                    {
                        Array.Clear(aesKey, 0, aesKey.Length);
                    }
                    if (aesIV != null)
                    {
                        Array.Clear(aesIV, 0, aesIV.Length);
                    }
                    if (authenticationKey != null)
                    {
                        Array.Clear(authenticationKey, 0, authenticationKey.Length);
                    }
                }

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
    ///     Combines multiple byte arrays into a single authenticated payload.
    /// </summary>
    /// <param name="encryptedKey">The RSA-encrypted AES key.</param>
    /// <param name="encryptedIV">The RSA-encrypted initialization vector.</param>
    /// <param name="encryptedAuthenticationKey">The RSA-encrypted authentication key.</param>
    /// <param name="authenticationTag">The HMAC tag covering the encrypted components.</param>
    /// <param name="encryptedData">The AES-encrypted data.</param>
    /// <returns>A single byte array containing all components.</returns>
    private static byte[] CombineEncryptedComponents(
        byte[] encryptedKey,
        byte[] encryptedIV,
        byte[] encryptedAuthenticationKey,
        byte[] authenticationTag,
        byte[] encryptedData)
    {
        var totalLength = 1 + encryptedKey.Length + encryptedIV.Length + encryptedAuthenticationKey.Length +
                          authenticationTag.Length + encryptedData.Length;
        var result = new byte[totalLength];

        var position = 0;

        result[position++] = FormatVersion;

        Buffer.BlockCopy(encryptedKey, 0, result, position, encryptedKey.Length);
        position += encryptedKey.Length;

        Buffer.BlockCopy(encryptedIV, 0, result, position, encryptedIV.Length);
        position += encryptedIV.Length;

        Buffer.BlockCopy(encryptedAuthenticationKey, 0, result, position, encryptedAuthenticationKey.Length);
        position += encryptedAuthenticationKey.Length;

        Buffer.BlockCopy(authenticationTag, 0, result, position, authenticationTag.Length);
        position += authenticationTag.Length;

        Buffer.BlockCopy(encryptedData, 0, result, position, encryptedData.Length);

        return result;
    }

    private static byte[] ComputeAuthenticationTag(
        byte[] authenticationKey,
        byte[] encryptedKey,
        byte[] encryptedIV,
        byte[] encryptedAuthenticationKey,
        byte[] encryptedData)
    {
        using var hmac = new HMACSHA256(authenticationKey);
        hmac.TransformBlock([FormatVersion], 0, 1, null, 0);
        hmac.TransformBlock(encryptedKey, 0, encryptedKey.Length, null, 0);
        hmac.TransformBlock(encryptedIV, 0, encryptedIV.Length, null, 0);
        hmac.TransformBlock(encryptedAuthenticationKey, 0, encryptedAuthenticationKey.Length, null, 0);
        hmac.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        return hmac.Hash!;
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
