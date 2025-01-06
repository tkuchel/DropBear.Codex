#region

using System.Security.Cryptography;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Helpers;

/// <summary>
///     Provides helper methods for hashing-related operations, such as salt generation and
///     combining/extracting byte arrays.
/// </summary>
public static class HashingHelper
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(HashingHelper));

    /// <summary>
    ///     Generates a random salt of the specified size in bytes.
    /// </summary>
    /// <param name="saltSize">The size of the salt to generate, in bytes.</param>
    /// <returns>A newly generated salt as a byte array.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="saltSize" /> is less than 1.</exception>
    public static byte[] GenerateRandomSalt(int saltSize)
    {
        if (saltSize < 1)
        {
            Logger.Error("Salt size must be at least 1 byte. Requested: {SaltSize}", saltSize);
            throw new ArgumentOutOfRangeException(nameof(saltSize), "Salt size must be at least 1.");
        }

        Logger.Information("Generating a random salt of size {SaltSize} bytes.", saltSize);

        var buffer = new byte[saltSize];
        try
        {
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(buffer);
            Logger.Debug("Random salt generated successfully: {SaltSize} bytes.", saltSize);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while generating random salt.");
            throw;
        }

        return buffer;
    }

    /// <summary>
    ///     Combines two byte arrays (salt + hash) into a single array.
    /// </summary>
    /// <param name="salt">The salt byte array. Cannot be null.</param>
    /// <param name="hash">The hash byte array. Cannot be null.</param>
    /// <returns>The combined byte array, with salt first, then hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="salt" /> or <paramref name="hash" /> is null.</exception>
    public static byte[] CombineBytes(byte[] salt, byte[] hash)
    {
        ArgumentNullException.ThrowIfNull(salt, nameof(salt));
        ArgumentNullException.ThrowIfNull(hash, nameof(hash));

        Logger.Information(
            "Combining salt and hash arrays. Salt size: {SaltSize} bytes, Hash size: {HashSize} bytes.",
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
    ///     Extracts salt and hash bytes from a combined byte array, assuming the salt is at the start.
    /// </summary>
    /// <param name="combinedBytes">The combined byte array containing salt + hash.</param>
    /// <param name="saltSize">The size (in bytes) of the salt portion.</param>
    /// <returns>
    ///     A tuple <c>(salt, hash)</c>, where <c>salt</c> is <paramref name="saltSize" /> bytes,
    ///     and <c>hash</c> is the remainder.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="combinedBytes" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown if <paramref name="saltSize" /> is invalid relative to <paramref name="combinedBytes" />.
    /// </exception>
    public static (byte[] salt, byte[] hash) ExtractBytes(byte[] combinedBytes, int saltSize)
    {
        ArgumentNullException.ThrowIfNull(combinedBytes, nameof(combinedBytes));
        if (saltSize < 0 || saltSize > combinedBytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(saltSize), "Salt size is invalid for the combined array.");
        }

        Logger.Information("Extracting salt and hash from combined byte array. Salt size: {SaltSize} bytes.", saltSize);

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
    ///     Converts a byte array to a Base64-encoded string.
    /// </summary>
    /// <param name="byteArray">The byte array to convert. Cannot be null.</param>
    /// <returns>A Base64-encoded string representing <paramref name="byteArray" />.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="byteArray" /> is null.</exception>
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
    ///     Converts a Base64-encoded string to a byte array.
    /// </summary>
    /// <param name="str">The Base64-encoded string. Cannot be null.</param>
    /// <returns>A byte array decoded from the Base64 string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="str" /> is null.</exception>
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
