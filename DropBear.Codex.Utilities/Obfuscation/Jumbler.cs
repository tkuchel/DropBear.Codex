#region

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
/// </summary>
public static class Jumbler
{
    private const string DefaultKeyPhrase = "QVANYCLEPW";
    private const string JumblePrefix = ".JuMbLe.02."; // Version identifier
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(Jumbler));

    // Cache derived keys for better performance
    private static readonly Dictionary<string, byte[]> KeyCache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Jumbles (obfuscates) a password using AES encryption.
    /// </summary>
    /// <param name="password">The password to jumble.</param>
    /// <param name="keyPhrase">Optional custom key phrase for encryption. If null, a default is used.</param>
    /// <returns>A Result containing the jumbled password string or error information.</returns>
    public static Result<string, JumblerError> JumblePassword(string password, string? keyPhrase = null)
    {
        if (string.IsNullOrEmpty(password))
        {
            Logger.Warning("Attempt to jumble null or empty password");
            return Result<string, JumblerError>.Failure(
                new JumblerError("Password cannot be null or empty."));
        }

        try
        {
            keyPhrase ??= DefaultKeyPhrase;

            // Get password bytes
            Span<byte> passwordBytes = Encoding.UTF8.GetBytes(password);

            // Get or derive key
            var keyBytes = GetOrDeriveKey(keyPhrase);

            // Encrypt the password
            var encryptedBytes = EncryptAes(passwordBytes, keyBytes);

            // Convert to Base64 and return without prefix (for backward compatibility)
            var jumbledValue = JumblePrefix + Convert.ToBase64String(encryptedBytes);
            return Result<string, JumblerError>.Success(jumbledValue[JumblePrefix.Length..]);
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
    /// </summary>
    /// <param name="password">The jumbled password to unjumble.</param>
    /// <param name="keyPhrase">Optional custom key phrase for decryption. If null, a default is used.</param>
    /// <returns>A Result containing the original password string or error information.</returns>
    public static Result<string, JumblerError> UnJumblePassword(string password, string? keyPhrase = null)
    {
        if (string.IsNullOrEmpty(password))
        {
            Logger.Warning("Attempt to unjumble null or empty password");
            return Result<string, JumblerError>.Failure(
                new JumblerError("Password cannot be null or empty."));
        }

        try
        {
            keyPhrase ??= DefaultKeyPhrase;

            // Handle passwords with or without the prefix
            var fullPassword = password.Contains(JumblePrefix, StringComparison.Ordinal)
                ? password
                : JumblePrefix + password;

            // Decode Base64
            var encryptedBytes = Convert.FromBase64String(fullPassword[JumblePrefix.Length..]);

            // Get or derive key
            var keyBytes = GetOrDeriveKey(keyPhrase);

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
    /// </summary>
    /// <param name="keyPhrase">The key phrase to derive the key from.</param>
    /// <returns>A 32-byte key for AES-256.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] GetOrDeriveKey(string keyPhrase)
    {
        // Check if we have a cached key for this phrase
        if (KeyCache.TryGetValue(keyPhrase, out var cachedKey))
        {
            return cachedKey;
        }

        // Derive a new key using PBKDF2
        using var deriveBytes = new Rfc2898DeriveBytes(
            keyPhrase,
            Encoding.UTF8.GetBytes(DefaultKeyPhrase),
            10000,
            HashAlgorithmName.SHA256);

        var key = deriveBytes.GetBytes(32); // 256 bits for AES-256

        // Cache the key for future use (for frequently used keyphrases)
        if (keyPhrase == DefaultKeyPhrase || KeyCache.Count < 10)
        {
            KeyCache[keyPhrase] = key;
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
    ///     Clears the internal key cache.
    /// </summary>
    public static void ClearKeyCache()
    {
        // Clear sensitive data from memory
        foreach (var key in KeyCache.Values)
        {
            Array.Clear(key, 0, key.Length);
        }

        KeyCache.Clear();
        Logger.Information("Key cache cleared");
    }
}
