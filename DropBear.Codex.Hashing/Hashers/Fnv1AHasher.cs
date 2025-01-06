#region

using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Hashing.Interfaces;
using HashDepot;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     A <see cref="IHasher" /> implementation using the FNV-1a hashing algorithm.
///     Simple and fast, but not cryptographically secure. No salt or iterations are used.
/// </summary>
public sealed class Fnv1AHasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Fnv1AHasher>();

    /// <summary>
    ///     FNV-1a does not support salt, so this is a no-op.
    /// </summary>
    public IHasher WithSalt(byte[]? salt)
    {
        return this;
    }

    /// <summary>
    ///     FNV-1a does not support iterations, so this is a no-op.
    /// </summary>
    public IHasher WithIterations(int iterations)
    {
        return this;
    }

    /// <summary>
    ///     Hashes the given input string using 32-bit FNV-1a. Returns a lowercase hex string (8 chars).
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A <see cref="Result{T}" /> containing the hex-encoded hash, or an error message.</returns>
    public Result<string> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("FNV-1a: Input cannot be null or empty.");
            return Result<string>.Failure("Input cannot be null or empty.");
        }

        try
        {
            Logger.Information("Hashing input with FNV-1a (32-bit).");
            var buffer = Encoding.UTF8.GetBytes(input);
            var hash = Fnv1a.Hash32(buffer);
            return Result<string>.Success(hash.ToString("x8"));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during FNV-1a hashing.");
            return Result<string>.Failure($"Error during hashing: {ex.Message}");
        }
    }

    /// <summary>
    ///     Verifies the input string by hashing it and comparing to <paramref name="expectedHash" />.
    /// </summary>
    /// <param name="input">The input string to verify.</param>
    /// <param name="expectedHash">A hex-encoded 32-bit FNV-1a hash.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public Result Verify(string input, string expectedHash)
    {
        var hashResult = Hash(input);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute FNV-1a hash for verification.");
            return Result.Failure("Failed to compute hash.");
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid ? "FNV-1a verification succeeded." : "FNV-1a verification failed.");

        return isValid ? Result.Success() : Result.Failure("Verification failed.");
    }

    /// <summary>
    ///     Computes a 64-bit FNV-1a hash of <paramref name="data" /> and returns it Base64-encoded.
    /// </summary>
    /// <param name="data">The byte array to hash.</param>
    /// <returns>A <see cref="Result{T}" /> with the Base64-encoded hash, or an error message.</returns>
    public Result<string> EncodeToBase64Hash(byte[] data)
    {
        if (data == Array.Empty<byte>() || data.Length is 0)
        {
            Logger.Error("FNV-1a: Data cannot be null or empty.");
            return Result<string>.Failure("Data cannot be null or empty.");
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using 64-bit FNV-1a.");
            var hash = Fnv1a.Hash64(data); // 64-bit for demonstration
            var hashBytes = BitConverter.GetBytes(hash);
            var base64Hash = Convert.ToBase64String(hashBytes);
            return Result<string>.Success(base64Hash);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during FNV-1a base64 encoding hash.");
            return Result<string>.Failure($"Error during base64 encoding hash: {ex.Message}");
        }
    }

    /// <summary>
    ///     Verifies <paramref name="data" /> by computing a 64-bit FNV-1a hash and comparing
    ///     it to <paramref name="expectedBase64Hash" />.
    /// </summary>
    /// <param name="data">The original byte array to hash.</param>
    /// <param name="expectedBase64Hash">A Base64-encoded FNV-1a 64-bit hash.</param>
    /// <returns>A <see cref="Result" /> indicating success if they match.</returns>
    public Result VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        var encodeResult = EncodeToBase64Hash(data);
        if (!encodeResult.IsSuccess)
        {
            Logger.Error("Failed to compute FNV-1a Base64 hash for verification.");
            return Result.Failure("Failed to compute hash.");
        }

        var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
        Logger.Information(isValid
            ? "FNV-1a Base64 hash verification succeeded."
            : "FNV-1a Base64 hash verification failed.");

        return isValid ? Result.Success() : Result.Failure("Base64 hash verification failed.");
    }

    /// <summary>
    ///     No-op for FNV-1a. It has a fixed or variable output (32/64-bit), but we do not dynamically change it.
    /// </summary>
    public IHasher WithHashSize(int size)
    {
        return this;
    }
}
