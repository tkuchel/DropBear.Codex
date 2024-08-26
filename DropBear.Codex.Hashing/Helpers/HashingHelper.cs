#region

using System.Security.Cryptography;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Helpers;

public static class HashingHelper
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(HashingHelper));

    /// <summary>
    ///     Generates a random salt of the specified size.
    /// </summary>
    /// <param name="saltSize">The size of the salt to generate.</param>
    /// <returns>The generated salt as a byte array.</returns>
    public static byte[] GenerateRandomSalt(int saltSize)
    {
        Logger.Information("Generating a random salt of size {SaltSize} bytes.", saltSize);

        var buffer = new byte[saltSize];
        try
        {
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(buffer);
            Logger.Debug("Random salt generated successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while generating random salt.");
            throw;
        }

        return buffer;
    }

    /// <summary>
    ///     Combines two byte arrays into one.
    /// </summary>
    /// <param name="salt">The salt byte array. Cannot be null.</param>
    /// <param name="hash">The hash byte array.</param>
    /// <returns>The combined byte array.</returns>
    public static byte[] CombineBytes(byte[] salt, byte[] hash)
    {
        ArgumentNullException.ThrowIfNull(salt, nameof(salt));
        ArgumentNullException.ThrowIfNull(hash, nameof(hash));

        Logger.Information("Combining salt and hash arrays. Salt size: {SaltSize} bytes, Hash size: {HashSize} bytes.",
            salt.Length, hash.Length);

        try
        {
            var combinedBytes = new byte[salt.Length + hash.Length];
            Buffer.BlockCopy(salt, 0, combinedBytes, 0, salt.Length);
            Buffer.BlockCopy(hash, 0, combinedBytes, salt.Length, hash.Length);
            Logger.Debug("Salt and hash combined successfully.");
            return combinedBytes;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while combining salt and hash arrays.");
            throw;
        }
    }

    /// <summary>
    ///     Extracts salt and hash bytes from a combined byte array.
    /// </summary>
    /// <param name="combinedBytes">The combined byte array containing salt and hash bytes.</param>
    /// <param name="saltSize">The size of the salt bytes.</param>
    /// <returns>A tuple containing salt and hash bytes.</returns>
    public static (byte[] salt, byte[] hash) ExtractBytes(byte[] combinedBytes, int saltSize)
    {
        ArgumentNullException.ThrowIfNull(combinedBytes, nameof(combinedBytes));

        Logger.Information("Extracting salt and hash from combined byte array. Salt size: {SaltSize} bytes.",
            saltSize);

        try
        {
            var salt = new byte[saltSize];
            var hash = new byte[combinedBytes.Length - saltSize];
            Buffer.BlockCopy(combinedBytes, 0, salt, 0, saltSize);
            Buffer.BlockCopy(combinedBytes, saltSize, hash, 0, hash.Length);
            Logger.Debug("Salt and hash extracted successfully.");
            return (salt, hash);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while extracting salt and hash from combined byte array.");
            throw;
        }
    }

    /// <summary>
    ///     Converts a byte array to a base64 string.
    /// </summary>
    /// <param name="byteArray">The byte array to convert.</param>
    /// <returns>The base64 string representation of the byte array.</returns>
    public static string ConvertByteArrayToBase64String(byte[] byteArray)
    {
        ArgumentNullException.ThrowIfNull(byteArray, nameof(byteArray));

        Logger.Information("Converting byte array to base64 string. Byte array size: {ByteArraySize} bytes.",
            byteArray.Length);

        try
        {
            var base64String = Convert.ToBase64String(byteArray);
            Logger.Debug("Byte array converted to base64 string successfully.");
            return base64String;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while converting byte array to base64 string.");
            throw;
        }
    }

    /// <summary>
    ///     Converts a base64 string to a byte array.
    /// </summary>
    /// <param name="str">The base64 string to convert.</param>
    /// <returns>The byte array representation of the base64 string.</returns>
    public static byte[] ConvertBase64StringToByteArray(string str)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        Logger.Information("Converting base64 string to byte array. String length: {StringLength} characters.",
            str.Length);

        try
        {
            var byteArray = Convert.FromBase64String(str);
            Logger.Debug("Base64 string converted to byte array successfully.");
            return byteArray;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while converting base64 string to byte array.");
            throw;
        }
    }
}
