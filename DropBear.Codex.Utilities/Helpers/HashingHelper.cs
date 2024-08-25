#region

using System.Security.Cryptography;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for cryptographic operations, including salt generation, byte array manipulation, and
///     Base64 conversions.
/// </summary>
public static class HashingHelper
{
    /// <summary>
    ///     Generates a cryptographically secure random salt of the specified size.
    /// </summary>
    /// <param name="saltSize">The size of the salt in bytes.</param>
    /// <returns>A byte array containing the generated salt.</returns>
    public static byte[] GenerateRandomSalt(int saltSize)
    {
        if (saltSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(saltSize), "Salt size must be a positive integer.");
        }

        var buffer = new byte[saltSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(buffer);
        return buffer;
    }

    /// <summary>
    ///     Combines two byte arrays into one.
    /// </summary>
    /// <param name="salt">The first byte array, typically a salt.</param>
    /// <param name="hash">The second byte array, typically a hash.</param>
    /// <returns>A combined byte array containing both the salt and the hash.</returns>
    public static byte[] CombineBytes(byte[] salt, byte[] hash)
    {
        if (salt == null)
        {
            throw new ArgumentNullException(nameof(salt));
        }

        if (hash == null)
        {
            throw new ArgumentNullException(nameof(hash));
        }

        var combinedBytes = new byte[salt.Length + hash.Length];
        Buffer.BlockCopy(salt, 0, combinedBytes, 0, salt.Length);
        Buffer.BlockCopy(hash, 0, combinedBytes, salt.Length, hash.Length);
        return combinedBytes;
    }

    /// <summary>
    ///     Extracts salt and hash bytes from a combined byte array.
    /// </summary>
    /// <param name="combinedBytes">The combined byte array containing salt and hash bytes.</param>
    /// <param name="saltSize">The size of the salt in bytes.</param>
    /// <returns>A tuple containing the extracted salt and hash byte arrays.</returns>
    public static (byte[] salt, byte[] hash) ExtractBytes(byte[] combinedBytes, int saltSize)
    {
        if (combinedBytes == null)
        {
            throw new ArgumentNullException(nameof(combinedBytes));
        }

        if (saltSize <= 0 || saltSize > combinedBytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(saltSize),
                "Salt size must be a positive integer and less than or equal to the length of the combined byte array.");
        }

        var salt = new byte[saltSize];
        var hash = new byte[combinedBytes.Length - saltSize];
        Buffer.BlockCopy(combinedBytes, 0, salt, 0, saltSize);
        Buffer.BlockCopy(combinedBytes, saltSize, hash, 0, hash.Length);
        return (salt, hash);
    }

    /// <summary>
    ///     Converts a byte array to its Base64 string representation.
    /// </summary>
    /// <param name="byteArray">The byte array to convert.</param>
    /// <returns>A Base64 string representing the byte array.</returns>
    public static string ConvertByteArrayToBase64String(byte[] byteArray)
    {
        if (byteArray == null)
        {
            throw new ArgumentNullException(nameof(byteArray));
        }

        return Convert.ToBase64String(byteArray);
    }

    /// <summary>
    ///     Converts a Base64 string back into a byte array.
    /// </summary>
    /// <param name="str">The Base64 string to convert.</param>
    /// <returns>A byte array represented by the Base64 string.</returns>
    /// <exception cref="FormatException">Thrown if the input string is not a valid Base64 string.</exception>
    public static byte[] ConvertBase64StringToByteArray(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            throw new ArgumentNullException(nameof(str));
        }

        return Convert.FromBase64String(str);
    }
}
