#region

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Converters;
using DropBear.Codex.Utilities.Errors;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Obfuscation;

/// <summary>
///     Provides simple methods for obfuscating and deobfuscating data.
///     Note: This is not a secure encryption mechanism and should only be used in non-security-critical scenarios.
/// </summary>
public static class SimpleObfuscator
{
    // Maximum size to cache
    private const int MaxCacheSize = 10240; // 10KB

    // Maximum cache entries
    private const int MaxCacheEntries = 100;
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(SimpleObfuscator));

    // Cached random sequences for common sizes
    private static readonly ConcurrentDictionary<int, List<int>> RandomSequenceCache = new();

    /// <summary>
    ///     Generates a SHA-256 hash for the provided data.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The hexadecimal representation of the hash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GenerateHash(string data)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var hashBytes = SHA256.HashData(dataBytes);

        // Use a Span-based approach for the hex conversion to reduce allocations
        Span<char> hexChars = stackalloc char[hashBytes.Length * 2];

        for (var i = 0; i < hashBytes.Length; i++)
        {
            var b = hashBytes[i];
            hexChars[i * 2] = GetHexChar(b >> 4);
            hexChars[(i * 2) + 1] = GetHexChar(b & 0xF);
        }

        return new string(hexChars);
    }

    /// <summary>
    ///     Converts a nibble to its hexadecimal character representation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char GetHexChar(int nibble)
    {
        return (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
    }

    /// <summary>
    ///     Encodes the specified value by obfuscating it and appending a hash for accidental corruption detection.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="key">Optional key for deterministic obfuscation. If null, a default key is used.</param>
    /// <returns>A Result containing the obfuscated and hashed value in hexadecimal format, or error information.</returns>
    public static Result<string, ObfuscationError> Encode(string value, string? key = null)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Result<string, ObfuscationError>.Failure(
                new ObfuscationError("Value to encode cannot be null or empty."));
        }

        // Convert to binary with Result pattern
        var binaryResult = BinaryAndHexConverter.StringToBinary(value);
        if (!binaryResult.IsSuccess)
        {
            return Result<string, ObfuscationError>.Failure(
                new ObfuscationError($"Failed to convert value to binary: {binaryResult.Error?.Message}"));
        }

        // Calculate hash for accidental corruption detection
        var hash = GenerateHash(value);

        // Convert hash to binary with Result pattern
        var hashBinaryResult = BinaryAndHexConverter.StringToBinary(hash);
        if (!hashBinaryResult.IsSuccess)
        {
            return Result<string, ObfuscationError>.Failure(
                new ObfuscationError($"Failed to convert hash to binary: {hashBinaryResult.Error?.Message}"));
        }

        // Combine value binary and hash binary
        var binary = binaryResult.Value!;
        var hashBinary = hashBinaryResult.Value!;
        var binaryWithHash = binary + hashBinary;

        // Generate deterministic pseudo-random sequence based on key
        var pseudoRandomSequence = GetPseudoRandomSequence(binaryWithHash.Length, key);

        // Use buffer pool for large strings to reduce allocations
        char[]? rentedArray = null;
        var resultBuffer = binaryWithHash.Length <= 1024
            ? stackalloc char[binaryWithHash.Length]
            : (rentedArray = ArrayPool<char>.Shared.Rent(binaryWithHash.Length)).AsSpan(0, binaryWithHash.Length);

        try
        {
            // Perform the obfuscation
            for (var i = 0; i < binaryWithHash.Length; i++)
            {
                resultBuffer[i] =
                    (char)('0' + ((binaryWithHash[i] == '0' ? 1 : 0) ^ (pseudoRandomSequence[i] % 2)));
            }

            var obfuscatedBinary = new string(resultBuffer);

            // Convert obfuscated binary to hex
            var hexResult = BinaryAndHexConverter.BinaryToHex(obfuscatedBinary);
            if (!hexResult.IsSuccess)
            {
                return Result<string, ObfuscationError>.Failure(
                    new ObfuscationError($"Failed to convert binary to hex: {hexResult.Error?.Message}"));
            }

            return Result<string, ObfuscationError>.Success(hexResult.Value!);
        }
        finally
        {
            // Return rented array to the pool if used
            if (rentedArray != null)
            {
                ArrayPool<char>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    ///     Decodes the specified obfuscated value and checks for accidental corruption.
    /// </summary>
    /// <param name="obfuscatedValue">The obfuscated value to decode.</param>
    /// <param name="key">Optional key for deterministic obfuscation. Must match the key used during encoding.</param>
    /// <returns>A Result containing the original value, or error information.</returns>
    public static Result<string, ObfuscationError> Decode(string obfuscatedValue, string? key = null)
    {
        if (string.IsNullOrEmpty(obfuscatedValue))
        {
            return Result<string, ObfuscationError>.Failure(
                new ObfuscationError("Obfuscated value cannot be null or empty."));
        }

        // Convert hex to binary with Result pattern
        var binaryResult = BinaryAndHexConverter.HexToBinary(obfuscatedValue);
        if (!binaryResult.IsSuccess)
        {
            return Result<string, ObfuscationError>.Failure(
                new ObfuscationError($"Failed to convert hex to binary: {binaryResult.Error?.Message}"));
        }

        var binary = binaryResult.Value!;

        // Get deterministic pseudo-random sequence based on key
        var pseudoRandomSequence = GetPseudoRandomSequence(binary.Length, key);

        // Use buffer pool for large strings to reduce allocations
        char[]? rentedArray = null;
        var resultBuffer = binary.Length <= 1024
            ? stackalloc char[binary.Length]
            : (rentedArray = ArrayPool<char>.Shared.Rent(binary.Length)).AsSpan(0, binary.Length);

        try
        {
            // Perform the deobfuscation
            for (var i = 0; i < binary.Length; i++)
            {
                resultBuffer[i] = (char)('0' + (((binary[i] - '0') ^ (pseudoRandomSequence[i] % 2)) == 0 ? 1 : 0));
            }

            var deobfuscatedBinary = new string(resultBuffer);

            // Split the binary into original value and hash
            if (deobfuscatedBinary.Length <= 256)
            {
                return Result<string, ObfuscationError>.Failure(
                    new ObfuscationError("Invalid obfuscated data: insufficient length for corruption detection."));
            }

            var originalBinary = deobfuscatedBinary[..^256];
            var hashBinary = deobfuscatedBinary[^256..];

            // Convert original binary back to string
            var originalValueResult = BinaryAndHexConverter.BinaryToString(originalBinary);
            if (!originalValueResult.IsSuccess)
            {
                return Result<string, ObfuscationError>.Failure(
                    new ObfuscationError(
                        $"Failed to convert binary to string: {originalValueResult.Error?.Message}"));
            }

            // Convert hash binary back to string
            var hashResult = BinaryAndHexConverter.BinaryToString(hashBinary);
            if (!hashResult.IsSuccess)
            {
                return Result<string, ObfuscationError>.Failure(
                    new ObfuscationError($"Failed to convert hash binary to string: {hashResult.Error?.Message}"));
            }

            var originalValue = originalValueResult.Value!;
            var hash = hashResult.Value!;

            // Check for accidental corruption by recalculating the hash
            if (!string.Equals(GenerateHash(originalValue), hash, StringComparison.Ordinal))
            {
                return Result<string, ObfuscationError>.Failure(
                    new ObfuscationError("Data corruption detection failed: hash mismatch."));
            }

            return Result<string, ObfuscationError>.Success(originalValue);
        }
        finally
        {
            // Return rented array to the pool if used
            if (rentedArray != null)
            {
                ArrayPool<char>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    ///     Generates or retrieves a cached deterministic pseudo-random sequence of integers for obfuscation.
    ///     The sequence is derived from the key using HKDF to ensure encode/decode produce the same sequence.
    /// </summary>
    /// <param name="length">The length of the sequence.</param>
    /// <param name="key">Optional key for deterministic generation. If null, a default key is used.</param>
    /// <returns>A list of deterministic pseudo-random integers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static List<int> GetPseudoRandomSequence(int length, string? key = null)
    {
        // Use a default key if none provided (for backward compatibility with light obfuscation)
        var effectiveKey = key ?? "DropBear.Codex.DefaultObfuscationKey";

        // Create a cache key that includes both length and key
        var cacheKey = HashCode.Combine(length, effectiveKey);

        // Return cached sequence if available and not too large
        if (length <= MaxCacheSize && RandomSequenceCache.TryGetValue(cacheKey, out var cachedSequence))
        {
            return cachedSequence;
        }

        // Generate deterministic sequence using HKDF
        var randomNumbers = new List<int>(length);
        var keyBytes = Encoding.UTF8.GetBytes(effectiveKey);

        // Use HKDF to derive a deterministic key
        var salt = Encoding.UTF8.GetBytes("DropBear.Codex.Obfuscation.Salt.v1");
        var info = Encoding.UTF8.GetBytes("DropBear.Codex.Obfuscation.Info.v1");

        // Calculate how many bytes we need (4 bytes per int)
        var bytesNeeded = length * 4;
        var derivedKey = new byte[bytesNeeded];

        // Use HKDF to derive deterministic bytes
        HKDF.DeriveKey(HashAlgorithmName.SHA256, keyBytes, derivedKey, salt, info);

        // Convert bytes to integers
        for (var i = 0; i < length; i++)
        {
            randomNumbers.Add(BitConverter.ToInt32(derivedKey, i * 4));
        }

        // Cache the sequence if not too large
        if (length <= MaxCacheSize && RandomSequenceCache.Count < MaxCacheEntries)
        {
            RandomSequenceCache[cacheKey] = randomNumbers;
        }

        return randomNumbers;
    }

    /// <summary>
    ///     Clears the internal random sequence cache.
    /// </summary>
    public static void ClearCache()
    {
        RandomSequenceCache.Clear();
        Logger.Information("Random sequence cache cleared");
    }
}
