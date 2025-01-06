#region

using System.Collections;
using System.Text;
using Blake2Fast;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Hashing.Helpers;
using DropBear.Codex.Hashing.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     A <see cref="IHasher" /> implementation using the Blake2b algorithm.
///     Supports an optional salt (combined with input) for hashing.
/// </summary>
public sealed class Blake2Hasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Blake2Hasher>();

    private int _hashSize = 32; // Default to 32 bytes for Blake2b
    private byte[]? _salt;

    /// <summary>
    ///     Sets the salt for the Blake2b hasher.
    /// </summary>
    /// <param name="salt">A byte array representing the salt. Must not be null or empty.</param>
    /// <returns>The current <see cref="Blake2Hasher" /> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="salt" /> is null or empty.</exception>
    public IHasher WithSalt(byte[]? salt)
    {
        if (salt is null || salt.Length == 0)
        {
            Logger.Error("Salt cannot be null or empty.");
            throw new ArgumentException("Salt cannot be null or empty.", nameof(salt));
        }

        Logger.Information("Setting salt for Blake2 hashing.");
        _salt = salt;
        return this;
    }

    /// <summary>
    ///     A no-op for Blake2. Iterations are not applicable in this algorithm.
    /// </summary>
    public IHasher WithIterations(int iterations)
    {
        Logger.Information("WithIterations called but not applicable for Blake2Hasher.");
        return this;
    }

    /// <summary>
    ///     Hashes the given input string using Blake2b.
    ///     If no salt is provided, a random salt is generated and prepended to the final hash.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A <see cref="Result{T}" /> with the Base64-encoded hash (salt + hash), or an error.</returns>
    public Result<string> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("Input cannot be null or empty.");
            return Result<string>.Failure("Input cannot be null or empty.");
        }

        try
        {
            Logger.Information("Hashing input using Blake2.");
            _salt ??= HashingHelper.GenerateRandomSalt(32);

            var hashBytes = HashWithBlake2(input, _salt);
            var combinedBytes = HashingHelper.CombineBytes(_salt, hashBytes);

            Logger.Information("Blake2 hashing successful.");
            return Result<string>.Success(Convert.ToBase64String(combinedBytes));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error computing Blake2 hash.");
            return Result<string>.Failure($"Error computing Blake2 hash: {ex.Message}");
        }
    }

    /// <summary>
    ///     Verifies the given input string against a Base64-encoded hash that includes the salt at the beginning.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="expectedHash">A Base64-encoded hash (salt + hashBytes) to verify against.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public Result Verify(string input, string expectedHash)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
        {
            Logger.Error("Input and expected hash cannot be null or empty.");
            return Result.Failure("Input and expected hash cannot be null or empty.");
        }

        if (_salt is null)
        {
            Logger.Error("Salt is required for verification.");
            return Result.Failure("Salt is required for verification.");
        }

        try
        {
            Logger.Information("Verifying Blake2 hash.");
            var expectedBytes = Convert.FromBase64String(expectedHash);

            var (salt, expectedHashBytes) = HashingHelper.ExtractBytes(expectedBytes, _salt.Length);
            var hashBytes = HashWithBlake2(input, salt);

            var isValid = StructuralComparisons.StructuralEqualityComparer.Equals(hashBytes, expectedHashBytes);
            if (isValid)
            {
                Logger.Information("Blake2 verification successful.");
                return Result.Success();
            }

            Logger.Warning("Blake2 verification failed.");
            return Result.Failure("Verification failed.");
        }
        catch (FormatException ex)
        {
            Logger.Error(ex, "Expected hash format is invalid for Blake2.");
            return Result.Failure("Expected hash format is invalid.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake2 verification.");
            return Result.Failure($"Error during verification: {ex.Message}");
        }
    }

    /// <summary>
    ///     Computes a Blake2b hash of the given byte array and returns it Base64-encoded.
    /// </summary>
    /// <param name="data">A byte array to hash.</param>
    /// <returns>A <see cref="Result{T}" /> with the Base64-encoded hash, or an error message.</returns>
    public Result<string> EncodeToBase64Hash(byte[] data)
    {
        if (data == Array.Empty<byte>() || data.Length == 0)
        {
            Logger.Error("Data cannot be null or empty.");
            return Result<string>.Failure("Data cannot be null or empty.");
        }

        Logger.Information("Encoding data to Base64 hash using Blake2.");
        var hash = Blake2b.ComputeHash(_hashSize, data);
        return Result<string>.Success(Convert.ToBase64String(hash));
    }

    /// <summary>
    ///     Verifies a byte array against a Base64-encoded Blake2b hash.
    /// </summary>
    /// <param name="data">The original byte array.</param>
    /// <param name="expectedBase64Hash">The Base64-encoded Blake2b hash to compare.</param>
    /// <returns>A <see cref="Result" /> indicating success if they match.</returns>
    public Result VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        if (data == Array.Empty<byte>() || data.Length == 0)
        {
            Logger.Error("Data cannot be null or empty.");
            return Result.Failure("Data cannot be null or empty.");
        }

        Logger.Information("Verifying Base64 hash using Blake2.");
        var hash = Blake2b.ComputeHash(_hashSize, data);
        var base64Hash = Convert.ToBase64String(hash);

        return string.Equals(base64Hash, expectedBase64Hash, StringComparison.Ordinal)
            ? Result.Success()
            : Result.Failure("Base64 hash verification failed.");
    }

    /// <summary>
    ///     Sets the Blake2b hash size (in bytes).
    /// </summary>
    /// <param name="size">Hash size in bytes (e.g., 16, 32, 64).</param>
    /// <returns>The current <see cref="Blake2Hasher" /> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="size" /> is less than 1.</exception>
    public IHasher WithHashSize(int size)
    {
        if (size < 1)
        {
            Logger.Error("Hash size must be at least 1 byte for Blake2.");
            throw new ArgumentOutOfRangeException(nameof(size), "Hash size must be at least 1 byte.");
        }

        Logger.Information("Setting hash size for Blake2 hashing to {Size} bytes.", size);
        _hashSize = size;
        return this;
    }

    private byte[] HashWithBlake2(string input, byte[]? salt)
    {
        Logger.Debug("Hashing input using Blake2 with optional salt.");
        var inputBytes = Encoding.UTF8.GetBytes(input);

        if (salt != null)
        {
            var saltedInput = HashingHelper.CombineBytes(salt, inputBytes);
            return Blake2b.ComputeHash(_hashSize, saltedInput);
        }

        return Blake2b.ComputeHash(_hashSize, inputBytes);
    }
}
