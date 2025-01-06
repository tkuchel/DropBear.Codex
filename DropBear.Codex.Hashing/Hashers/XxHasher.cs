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
///     A <see cref="IHasher" /> implementation using the XXHash (64-bit version).
///     Does not support salt or iterations.
/// </summary>
public sealed class XxHasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<XxHasher>();
    private ulong _seed; // 0 by default

    /// <summary>
    ///     Initializes a new instance of the <see cref="XxHasher" /> class with an optional seed.
    /// </summary>
    /// <param name="seed">A 64-bit seed for XXHash.</param>
    public XxHasher(ulong seed = 0)
    {
        _seed = seed;
    }

    /// <summary>
    ///     XXHash does not support salt, so this is a no-op.
    /// </summary>
    public IHasher WithSalt(byte[]? salt)
    {
        return this;
    }

    /// <summary>
    ///     XXHash does not support iterations, so this is a no-op.
    /// </summary>
    public IHasher WithIterations(int iterations)
    {
        return this;
    }

    /// <summary>
    ///     Hashes the input string using 64-bit XXHash, returning a lowercase hex string (8 chars).
    /// </summary>
    /// <param name="input">The input to hash.</param>
    /// <returns>A <see cref="Result{T}" /> containing the hex-encoded hash.</returns>
    public Result<string> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("XXHash: Input cannot be null or empty.");
            return Result<string>.Failure("Input cannot be null or empty.");
        }

        try
        {
            Logger.Information("Hashing input with XXHash (64-bit).");
            var buffer = Encoding.UTF8.GetBytes(input);
            var hash = XXHash.Hash64(buffer, _seed);
            return Result<string>.Success(hash.ToString("x8"));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during XXHash hashing.");
            return Result<string>.Failure($"Error during hashing: {ex.Message}");
        }
    }

    /// <summary>
    ///     Verifies the input string by hashing with XXHash and comparing to <paramref name="expectedHash" />.
    /// </summary>
    /// <param name="input">The original input string.</param>
    /// <param name="expectedHash">A lowercase hex string representing a 64-bit XXHash.</param>
    /// <returns>A <see cref="Result" /> indicating success if they match.</returns>
    public Result Verify(string input, string expectedHash)
    {
        var hashResult = Hash(input);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute XXHash for verification.");
            return Result.Failure("Failed to compute hash.");
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.Ordinal);
        Logger.Information(isValid ? "XXHash verification succeeded." : "XXHash verification failed.");

        return isValid ? Result.Success() : Result.Failure("Verification failed.");
    }

    /// <summary>
    ///     Computes a 64-bit XXHash on <paramref name="data" /> and returns the result Base64-encoded.
    /// </summary>
    /// <param name="data">The byte array to hash.</param>
    /// <returns>A <see cref="Result{T}" /> with the Base64-encoded hash, or an error.</returns>
    public Result<string> EncodeToBase64Hash(byte[] data)
    {
        if (data == Array.Empty<byte>() || data.Length is 0)
        {
            Logger.Error("Data cannot be null or empty for XXHash encoding.");
            return Result<string>.Failure("Data cannot be null or empty.");
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using XXHash (64-bit).");
            var hash = XXHash.Hash64(data, _seed);
            var hashBytes = BitConverter.GetBytes(hash);
            var base64Hash = Convert.ToBase64String(hashBytes);

            return Result<string>.Success(base64Hash);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during XXHash Base64 encoding.");
            return Result<string>.Failure($"Error during base64 encoding hash: {ex.Message}");
        }
    }

    /// <summary>
    ///     Verifies the given byte array by computing a 64-bit XXHash and comparing it
    ///     to <paramref name="expectedBase64Hash" />.
    /// </summary>
    /// <param name="data">The original byte array.</param>
    /// <param name="expectedBase64Hash">The Base64-encoded 64-bit XXHash.</param>
    /// <returns>A <see cref="Result" /> indicating success if they match.</returns>
    public Result VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        var encodeResult = EncodeToBase64Hash(data);
        if (!encodeResult.IsSuccess)
        {
            Logger.Error("Failed to compute XXHash Base64 hash for verification.");
            return Result.Failure("Failed to compute hash.");
        }

        var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
        Logger.Information(isValid
            ? "XXHash Base64 hash verification succeeded."
            : "XXHash Base64 hash verification failed.");

        return isValid ? Result.Success() : Result.Failure("Base64 hash verification failed.");
    }

    /// <summary>
    ///     No-op for XXHash. Output size is typically 32-bit or 64-bit, so we do not dynamically adjust it.
    /// </summary>
    public IHasher WithHashSize(int size)
    {
        return this;
    }

    /// <summary>
    ///     Sets the seed for XXHash operations.
    /// </summary>
    /// <param name="seed">A 64-bit seed.</param>
    /// <returns>The current <see cref="XxHasher" /> instance.</returns>
    public IHasher WithSeed(ulong seed)
    {
        Logger.Information("Setting seed for XXHasher to {Seed}.", seed);
        _seed = seed;
        return this;
    }
}
