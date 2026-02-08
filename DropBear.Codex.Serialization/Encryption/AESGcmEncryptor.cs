#region

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Serialization.Encryption;

/// <summary>
///     Provides methods to encrypt and decrypt data using AES-GCM encryption.
/// </summary>
public sealed partial class AesGcmEncryptor : IEncryptor, IDisposable
{
    private const int KeySize = 32; // AES-256 key size in bytes
    private const int TagSize = 16; // GCM tag size in bytes
    private const int NonceSize = 12; // Recommended for GCM

    private readonly byte[] _key;

    private readonly ILogger<AesGcmEncryptor> _logger;
    private readonly RSA _rsa;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AesGcmEncryptor" /> class with the specified RSA key pair.
    /// </summary>
    /// <param name="rsa">The RSA key pair used for encryption.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="enableCaching">Deprecated parameter, no longer used. Caching is disabled for security.</param>
    /// <param name="maxCacheSize">Deprecated parameter, no longer used.</param>
    public AesGcmEncryptor(RSA rsa, ILogger<AesGcmEncryptor> logger, bool enableCaching = false, int maxCacheSize = 100)
    {
        _rsa = rsa ?? throw new ArgumentNullException(nameof(rsa), "RSA key pair cannot be null.");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _key = GenerateKey();

        // SECURITY: Encryption caching has been removed to prevent nonce reuse vulnerabilities
        // AES-GCM requires a unique nonce for every encryption operation
        LogEncryptorInitialized(_logger);
    }

    /// <summary>
    ///     Disposes the encryptor and securely erases sensitive cryptographic material.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        LogDisposingEncryptor(_logger);

        // Securely erase the key
        if (_key.Length > 0)
        {
            Array.Clear(_key, 0, _key.Length);
        }

        // Dispose of RSA resources
        _rsa.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Encrypts the specified data using AES-GCM encryption.
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

            LogEncryptionStarting(_logger, data.Length);
            var stopwatch = Stopwatch.StartNew();

            // Rent buffers from ArrayPool for better performance
            var nonce = ArrayPool<byte>.Shared.Rent(NonceSize);
            var ciphertext = ArrayPool<byte>.Shared.Rent(data.Length);
            var tag = ArrayPool<byte>.Shared.Rent(TagSize);

            try
            {
                // Generate random nonce
                RandomNumberGenerator.Fill(nonce.AsSpan(0, NonceSize));

                // Perform encryption
                using var aesGcm = new AesGcm(_key, TagSize);
                aesGcm.Encrypt(nonce.AsSpan(0, NonceSize), data, ciphertext.AsSpan(0, data.Length), tag.AsSpan(0, TagSize));

                // Encrypt the key and nonce with RSA
                var encryptedKey = await EncryptWithRsaAsync(_key, cancellationToken).ConfigureAwait(false);
                var encryptedNonce = await EncryptWithRsaAsync(nonce.AsSpan(0, NonceSize).ToArray(), cancellationToken).ConfigureAwait(false);

                // Combine everything into a single result
                var combinedData = CombineEncryptedComponents(encryptedKey, encryptedNonce, tag.AsSpan(0, TagSize), ciphertext.AsSpan(0, data.Length));

                stopwatch.Stop();
                LogEncryptionCompleted(_logger, stopwatch.ElapsedMilliseconds, data.Length, combinedData.Length);

                return Result<byte[], SerializationError>.Success(combinedData);
            }
            catch (CryptographicException ex)
            {
                stopwatch.Stop();
                LogCryptographicErrorEncryption(_logger, ex, ex.Message);
                return Result<byte[], SerializationError>.Failure(
                    new SerializationError($"AES-GCM encryption failed: {ex.Message}") { Operation = "Encrypt" }, ex);
            }
            finally
            {
                // Return rented buffers to the pool and clear sensitive data
                Array.Clear(nonce, 0, NonceSize);
                Array.Clear(tag, 0, TagSize);
                Array.Clear(ciphertext, 0, data.Length);

                ArrayPool<byte>.Shared.Return(nonce);
                ArrayPool<byte>.Shared.Return(ciphertext);
                ArrayPool<byte>.Shared.Return(tag);
            }
        }
        catch (Exception ex)
        {
            LogEncryptionError(_logger, ex, ex.Message);
            return Result<byte[], SerializationError>.Failure(
                new SerializationError($"Encryption error: {ex.Message}") { Operation = "Encrypt" }, ex);
        }
    }

    /// <summary>
    ///     Decrypts the specified AES-GCM encrypted data.
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

            LogDecryptionStarting(_logger, data.Length);
            var stopwatch = Stopwatch.StartNew();

            byte[]? decryptedKey = null;
            byte[]? decryptedNonce = null;

            try
            {
                // Extract key, nonce, tag, and ciphertext from combined data
                var keySizeBytes = _rsa.KeySize / 8;

                if (data.Length <= (2 * keySizeBytes) + TagSize)
                {
                    throw new CryptographicException("Encrypted data is too short to contain required components");
                }

                // Store offsets for later span creation (cannot use spans across await)
                var tagOffset = 2 * keySizeBytes;
                var ciphertextOffset = tagOffset + TagSize;
                var ciphertextLength = data.Length - ciphertextOffset;

                // Extract and decrypt key and nonce (need arrays for RSA)
                var encryptedKey = data.AsSpan(0, keySizeBytes).ToArray();
                var encryptedNonce = data.AsSpan(keySizeBytes, keySizeBytes).ToArray();

                decryptedKey = await DecryptWithRsaAsync(encryptedKey, cancellationToken).ConfigureAwait(false);
                decryptedNonce = await DecryptWithRsaAsync(encryptedNonce, cancellationToken).ConfigureAwait(false);

                // Now recreate spans after async operations (safe to use in synchronous code below)
                var tagSpan = data.AsSpan(tagOffset, TagSize);
                var ciphertextSpan = data.AsSpan(ciphertextOffset, ciphertextLength);

                // Rent buffer for plaintext from ArrayPool
                var plaintextBuffer = ArrayPool<byte>.Shared.Rent(ciphertextLength);
                try
                {
                    // Decrypt the data (synchronous operation)
                    using var aesGcm = new AesGcm(decryptedKey, TagSize);
                    aesGcm.Decrypt(decryptedNonce, ciphertextSpan, tagSpan, plaintextBuffer.AsSpan(0, ciphertextLength));

                    // Copy to final result array (this allocation is necessary for the return value)
                    var plaintext = new byte[ciphertextLength];
                    plaintextBuffer.AsSpan(0, ciphertextLength).CopyTo(plaintext);

                    stopwatch.Stop();
                    LogDecryptionCompleted(_logger, stopwatch.ElapsedMilliseconds, data.Length, plaintext.Length);

                    return Result<byte[], SerializationError>.Success(plaintext);
                }
                finally
                {
                    // Clear and return the rented buffer
                    Array.Clear(plaintextBuffer, 0, ciphertextLength);
                    ArrayPool<byte>.Shared.Return(plaintextBuffer);
                }
            }
            catch (CryptographicException ex)
            {
                stopwatch.Stop();
                LogCryptographicErrorDecryption(_logger, ex, ex.Message);
                return Result<byte[], SerializationError>.Failure(
                    new SerializationError($"AES-GCM decryption failed: {ex.Message}") { Operation = "Decrypt" }, ex);
            }
            finally
            {
                // Securely clear decrypted key and nonce
                if (decryptedKey != null)
                {
                    Array.Clear(decryptedKey, 0, decryptedKey.Length);
                }
                if (decryptedNonce != null)
                {
                    Array.Clear(decryptedNonce, 0, decryptedNonce.Length);
                }
            }
        }
        catch (Exception ex)
        {
            LogDecryptionError(_logger, ex, ex.Message);
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
    ///     Combines multiple byte arrays and spans into a single array using efficient copying.
    /// </summary>
    /// <param name="encryptedKey">The RSA-encrypted AES key.</param>
    /// <param name="encryptedNonce">The RSA-encrypted nonce.</param>
    /// <param name="tag">The GCM authentication tag.</param>
    /// <param name="ciphertext">The encrypted data.</param>
    /// <returns>A single byte array containing all components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] CombineEncryptedComponents(byte[] encryptedKey, byte[] encryptedNonce, ReadOnlySpan<byte> tag,
        ReadOnlySpan<byte> ciphertext)
    {
        var totalLength = encryptedKey.Length + encryptedNonce.Length + tag.Length + ciphertext.Length;
        var result = new byte[totalLength];
        var resultSpan = result.AsSpan();

        var position = 0;

        encryptedKey.AsSpan().CopyTo(resultSpan.Slice(position));
        position += encryptedKey.Length;

        encryptedNonce.AsSpan().CopyTo(resultSpan.Slice(position));
        position += encryptedNonce.Length;

        tag.CopyTo(resultSpan.Slice(position));
        position += tag.Length;

        ciphertext.CopyTo(resultSpan.Slice(position));

        return result;
    }

    /// <summary>
    ///     Encrypts data using RSA with OAEP SHA-256 padding.
    /// </summary>
    /// <param name="data">The data to encrypt with RSA.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The RSA encrypted data.</returns>
    private async Task<byte[]> EncryptWithRsaAsync(byte[] data, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Decrypts data using RSA with OAEP SHA-256 padding.
    /// </summary>
    /// <param name="encryptedData">The RSA encrypted data to decrypt.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The decrypted data.</returns>
    private async Task<byte[]> DecryptWithRsaAsync(byte[] encryptedData, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _rsa.Decrypt(encryptedData, RSAEncryptionPadding.OaepSHA256);
        }, cancellationToken).ConfigureAwait(false);
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

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Information, Message = "AesGcmEncryptor initialized. Encryption caching is disabled for security.")]
    static partial void LogEncryptorInitialized(ILogger<AesGcmEncryptor> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Disposing AesGcmEncryptor and securely erasing the key.")]
    static partial void LogDisposingEncryptor(ILogger<AesGcmEncryptor> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting encryption of data with length {Length} bytes.")]
    static partial void LogEncryptionStarting(ILogger<AesGcmEncryptor> logger, int length);

    [LoggerMessage(Level = LogLevel.Information, Message = "Encryption completed successfully in {ElapsedMs}ms. Original size: {OriginalSize}, Encrypted size: {EncryptedSize}")]
    static partial void LogEncryptionCompleted(ILogger<AesGcmEncryptor> logger, long elapsedMs, int originalSize, int encryptedSize);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting decryption of data with length {Length} bytes.")]
    static partial void LogDecryptionStarting(ILogger<AesGcmEncryptor> logger, int length);

    [LoggerMessage(Level = LogLevel.Information, Message = "Decryption completed successfully in {ElapsedMs}ms. Encrypted size: {EncryptedSize}, Decrypted size: {DecryptedSize}")]
    static partial void LogDecryptionCompleted(ILogger<AesGcmEncryptor> logger, long elapsedMs, int encryptedSize, int decryptedSize);

    [LoggerMessage(Level = LogLevel.Error, Message = "Cryptographic error during encryption: {Message}")]
    static partial void LogCryptographicErrorEncryption(ILogger<AesGcmEncryptor> logger, Exception ex, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Cryptographic error during decryption: {Message}")]
    static partial void LogCryptographicErrorDecryption(ILogger<AesGcmEncryptor> logger, Exception ex, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred during encryption: {Message}")]
    static partial void LogEncryptionError(ILogger<AesGcmEncryptor> logger, Exception ex, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred during decryption: {Message}")]
    static partial void LogDecryptionError(ILogger<AesGcmEncryptor> logger, Exception ex, string message);

    #endregion
}
