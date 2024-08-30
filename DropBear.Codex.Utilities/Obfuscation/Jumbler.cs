#region

using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Utilities.Exceptions;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Obfuscation;

public static class Jumbler
{
    private const string DefaultKeyPhrase = "QVANYCLEPW";
    private const string JumblePrefix = ".JuMbLe.02."; // Updated version number
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(Jumbler));

    public static string JumblePassword(string password, string? keyPhrase = null)
    {
        if (string.IsNullOrEmpty(password))
        {
            Logger.Warning("Attempt to jumble null or empty password");
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        }

        try
        {
            keyPhrase ??= DefaultKeyPhrase;
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var keyBytes = DeriveKey(keyPhrase);
            var encryptedBytes = EncryptAes(passwordBytes, keyBytes);
            var jumbledValue = JumblePrefix + Convert.ToBase64String(encryptedBytes);
            return jumbledValue[JumblePrefix.Length..];
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error jumbling password");
            throw new JumblerException("Failed to jumble password", ex);
        }
    }

    public static string UnJumblePassword(string password, string? keyPhrase = null)
    {
        if (string.IsNullOrEmpty(password))
        {
            Logger.Warning("Attempt to unjumble null or empty password");
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        }

        try
        {
            keyPhrase ??= DefaultKeyPhrase;
            var fullPassword = password.Contains(JumblePrefix) ? password : JumblePrefix + password;
            var encryptedBytes = Convert.FromBase64String(fullPassword[JumblePrefix.Length..]);
            var keyBytes = DeriveKey(keyPhrase);
            var decryptedBytes = DecryptAes(encryptedBytes, keyBytes);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error unjumbling password");
            throw new JumblerException("Failed to unjumble password", ex);
        }
    }

    private static byte[] DeriveKey(string keyPhrase)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(keyPhrase, Encoding.UTF8.GetBytes(DefaultKeyPhrase), 10000,
            HashAlgorithmName.SHA256);
        return deriveBytes.GetBytes(32); // 256 bits for AES-256
    }

    private static byte[] EncryptAes(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
        }

        return ms.ToArray();
    }

    private static byte[] DecryptAes(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(data, 0, iv, 0, iv.Length);
        using var decryptor = aes.CreateDecryptor(aes.Key, iv);
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
        {
            cs.Write(data, iv.Length, data.Length - iv.Length);
        }

        return ms.ToArray();
    }
}
