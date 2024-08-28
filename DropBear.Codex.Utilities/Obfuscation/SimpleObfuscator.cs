#region

using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Utilities.Converters;

#endregion

namespace DropBear.Codex.Utilities.Obfuscation;

/// <summary>
///     Provides simple methods for obfuscating and deobfuscating data.
///     Note: This is not a secure encryption mechanism and should only be used in non-security-critical scenarios.
/// </summary>
public static class SimpleObfuscator
{
    /// <summary>
    ///     Generates a SHA-256 hash for the provided data.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The hexadecimal representation of the hash.</returns>
    private static string GenerateHash(string data)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Encodes the specified value by obfuscating it and appending a hash for integrity verification.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>The obfuscated and hashed value in hexadecimal format.</returns>
    public static string Encode(string value)
    {
        var binary = BinaryAndHexConverter.StringToBinary(value);
        var hash = GenerateHash(value);
        var hashBinary = BinaryAndHexConverter.StringToBinary(hash);

        var binaryWithHash = binary + hashBinary;
        var pseudoRandomSequence = GeneratePseudoRandomSequence(binaryWithHash.Length);

        var obfuscatedBinary = new StringBuilder(binaryWithHash.Length);
        for (var i = 0; i < binaryWithHash.Length; i++)
        {
            obfuscatedBinary.Append((binaryWithHash[i] == '0' ? '1' : '0') ^ (pseudoRandomSequence[i] % 2));
        }

        return BinaryAndHexConverter.BinaryToHex(obfuscatedBinary.ToString());
    }

    /// <summary>
    ///     Decodes the specified obfuscated value and verifies its integrity.
    /// </summary>
    /// <param name="obfuscatedValue">The obfuscated value to decode.</param>
    /// <returns>The original value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if data integrity check fails.</exception>
    public static string Decode(string obfuscatedValue)
    {
        var binary = BinaryAndHexConverter.HexToBinary(obfuscatedValue);
        var pseudoRandomSequence = GeneratePseudoRandomSequence(binary.Length);

        var deobfuscatedBinary = new StringBuilder(binary.Length);
        for (var i = 0; i < binary.Length; i++)
        {
            deobfuscatedBinary.Append((binary[i] ^ (pseudoRandomSequence[i] % 2)) == '0' ? '1' : '0');
        }

        var originalBinary = deobfuscatedBinary.ToString()[..^256];
        var hashBinary = deobfuscatedBinary.ToString()[^256..];
        var originalValue = BinaryAndHexConverter.BinaryToString(originalBinary);
        var hash = BinaryAndHexConverter.BinaryToString(hashBinary);

        if (!string.Equals(GenerateHash(originalValue), hash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Data integrity check failed - hash mismatch.");
        }

        return originalValue;
    }

    /// <summary>
    ///     Generates a pseudo-random sequence of integers for obfuscation.
    /// </summary>
    /// <param name="length">The length of the sequence.</param>
    /// <returns>A list of random integers.</returns>
    private static List<int> GeneratePseudoRandomSequence(int length)
    {
        using var rng = RandomNumberGenerator.Create();
        var randomNumber = new byte[4];
        var randomNumbers = new List<int>(length);

        for (var i = 0; i < length; i++)
        {
            rng.GetBytes(randomNumber);
            randomNumbers.Add(BitConverter.ToInt32(randomNumber, 0));
        }

        return randomNumbers;
    }
}
