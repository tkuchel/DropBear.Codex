#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Obfuscation;

/// <summary>
///     Provides methods for obfuscating and deobfuscating passwords using AES encryption.
///     SECURITY: This version uses 600,000 PBKDF2 iterations and random salts per operation.
/// </summary>
public static class Jumbler
{
    private const string JumblePrefix = ".JuMbLe.03."; // Version identifier (v03 = 600k iterations + random salts)
    private const int Pbkdf2Iterations = 600_000; // OWASP 2023 recommendation
    private const int SaltSize = 32; // 256 bits
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(Jumbler));

    // Cache derived keys for better performance (thread-safe)
    private static readonly ConcurrentDictionary<string, byte[]> KeyCache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Jumbles (obfuscates) a password using AES encryption.
    ///     SECURITY: Requires explicit keyPhrase. Uses 600,000 PBKDF2 iterations and random salt per operation.
    /// </summary>
    /// <param name="password">The password to jumble.</param>
    /// <param name="keyPhrase">Required key phrase for encryption. Must not be null or empty.</param>
    /// <returns>A Result containing the jumbled password string or error information.</returns>
    public static Result<string, JumblerError> JumblePassword(string password, string keyPhrase)
    {
        if (string.IsNullOrEmpty(password))
        {
            Logger.Warning("Attempt to jumble null or empty password");
            return Result<string, JumblerError>.Failure(
                new JumblerError("Password cannot be null or empty."));
        }

        if (string.IsNullOrWhiteSpace(keyPhrase))
        {
            Logger.Error("KeyPhrase is required for security. Refusing to use default key.");
            return Result<string, JumblerError>.Failure(
                new JumblerError("KeyPhrase must be provided. Default keys are not supported for security reasons."));
        }

        try
        {
            // Generate random salt for PBKDF2
            var salt = RandomNumberGenerator.GetBytes(SaltSize);

            // Get password bytes
            Span<byte> passwordBytes = Encoding.UTF8.GetBytes(password);

            // Derive key using random salt
            var keyBytes = DeriveKey(keyPhrase, salt);

            // Encrypt the password
            var encryptedBytes = EncryptAes(passwordBytes, keyBytes);

            // Combine: salt + encrypted data
            var combinedBytes = new byte[salt.Length + encryptedBytes.Length];
            Buffer.BlockCopy(salt, 0, combinedBytes, 0, salt.Length);
            Buffer.BlockCopy(encryptedBytes, 0, combinedBytes, salt.Length, encryptedBytes.Length);

            // Convert to Base64 with version prefix
            var jumbledValue = JumblePrefix + Convert.ToBase64String(combinedBytes);
            return Result<string, JumblerError>.Success(jumbledValue);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error jumbling password");
            return Result<string, JumblerError>.Failure(
                new JumblerError($"Failed to jumble password: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Unjumbles (deobfuscates) a previously jumbled password.
    ///     SECURITY: Requires explicit keyPhrase. Supports v03 format with random salts.
    /// </summary>
    /// <param name="password">The jumbled password to unjumble.</param>
    /// <param name="keyPhrase">Required key phrase for decryption. Must not be null or empty.</param>
    /// <returns>A Result containing the original password string or error information.</returns>
    public static Result<string, JumblerError> UnJumblePassword(string password, string keyPhrase)
    {
        if (string.IsNullOrEmpty(password))
        {
            Logger.Warning("Attempt to unjumble null or empty password");
            return Result<string, JumblerError>.Failure(
                new JumblerError("Password cannot be null or empty."));
        }

        if (string.IsNullOrWhiteSpace(keyPhrase))
        {
            Logger.Error("KeyPhrase is required for security. Refusing to use default key.");
            return Result<string, JumblerError>.Failure(
                new JumblerError("KeyPhrase must be provided. Default keys are not supported for security reasons."));
        }

        try
        {
            // Handle passwords with or without the prefix
            var fullPassword = password.Contains(JumblePrefix, StringComparison.Ordinal)
                ? password
                : JumblePrefix + password;

            // Decode Base64
            var combinedBytes = Convert.FromBase64String(fullPassword[JumblePrefix.Length..]);

            // Extract salt (first SaltSize bytes) and encrypted data
            if (combinedBytes.Length < SaltSize)
            {
                return Result<string, JumblerError>.Failure(
                    new JumblerError("Invalid jumbled password format: too short to contain salt."));
            }

            var salt = new byte[SaltSize];
            var encryptedBytes = new byte[combinedBytes.Length - SaltSize];
            Buffer.BlockCopy(combinedBytes, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(combinedBytes, SaltSize, encryptedBytes, 0, encryptedBytes.Length);

            // Derive key using extracted salt
            var keyBytes = DeriveKey(keyPhrase, salt);

            // Decrypt
            var decryptedBytes = DecryptAes(encryptedBytes, keyBytes);

            return Result<string, JumblerError>.Success(Encoding.UTF8.GetString(decryptedBytes));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error unjumbling password");
            return Result<string, JumblerError>.Failure(
                new JumblerError($"Failed to unjumble password: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Derives a cryptographic key from a key phrase using PBKDF2 with SHA256.
    ///     Uses 600,000 iterations (OWASP 2023 recommendation) and provided salt.
    /// </summary>
    /// <param name="keyPhrase">The key phrase to derive the key from.</param>
    /// <param name="salt">The salt bytes for PBKDF2.</param>
    /// <returns>A 32-byte key for AES-256.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] DeriveKey(string keyPhrase, byte[] salt)
    {
        // Create a cache key from keyPhrase + salt
        var cacheKey = $"{keyPhrase}:{Convert.ToBase64String(salt)}";

        // Check if we have a cached key (thread-safe)
        if (KeyCache.TryGetValue(cacheKey, out var cachedKey))
        {
            return cachedKey;
        }

        // Derive a new key using PBKDF2 with 600,000 iterations
        using var deriveBytes = new Rfc2898DeriveBytes(
            keyPhrase,
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256);

        var key = deriveBytes.GetBytes(32); // 256 bits for AES-256

        // Cache the key for future use (limit cache size to prevent memory issues)
        if (KeyCache.Count < 100)
        {
            KeyCache.TryAdd(cacheKey, key);
        }

        return key;
    }

    /// <summary>
    ///     Encrypts data using AES in CBC mode with a random IV.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <returns>Encrypted bytes including the IV.</returns>
    private static byte[] EncryptAes(ReadOnlySpan<byte> data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();

        // Write the IV to the beginning of the output
        ms.Write(aes.IV, 0, aes.IV.Length);

        // Encrypt the data
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(data);
        }

        return ms.ToArray();
    }

    /// <summary>
    ///     Decrypts data using AES in CBC mode, extracting the IV from the encrypted data.
    /// </summary>
    /// <param name="data">The encrypted data including the IV.</param>
    /// <param name="key">The decryption key.</param>
    /// <returns>Decrypted bytes.</returns>
    private static byte[] DecryptAes(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;

        // Extract the IV from the beginning of the data
        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(data, 0, iv, 0, iv.Length);

        // Create the decryptor
        using var decryptor = aes.CreateDecryptor(aes.Key, iv);
        using var ms = new MemoryStream();

        // Decrypt the data
        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
        {
            cs.Write(data, iv.Length, data.Length - iv.Length);
        }

        return ms.ToArray();
    }

    /// <summary>
    ///     Clears the internal key cache (thread-safe).
    /// </summary>
    public static void ClearKeyCache()
    {
        // Clear sensitive data from memory (thread-safe iteration)
        var cachedKeys = KeyCache.Values.ToArray();
        foreach (var key in cachedKeys)
        {
            Array.Clear(key, 0, key.Length);
        }

        KeyCache.Clear();
        Logger.Information("Key cache cleared");
    }
}
