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
///     A <see cref="IHasher" /> implementation using the MurmurHash3 algorithm (32-bit version by default).
///     MurmurHash3 does not support salt or iterations, so related methods are no-ops.
/// </summary>
public sealed class Murmur3Hasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Murmur3Hasher>();
    private uint _seed; // Allows customization via fluent API

    /// <summary>
    ///     Initializes a new instance of the <see cref="Murmur3Hasher" /> class with an optional seed.
    /// </summary>
    /// <param name="seed">The seed for the MurmurHash3 algorithm.</param>
    public Murmur3Hasher(uint seed = 0)
    {
        _seed = seed;
    }

    /// <summary>
    ///     No-op for <see cref="Murmur3Hasher" />. Salt is unsupported.
    /// </summary>
    public IHasher WithSalt(byte[]? salt)
    {
        return this;
    }

    /// <summary>
    ///     No-op for <see cref="Murmur3Hasher" />. Iterations are unsupported.
    /// </summary>
    public IHasher WithIterations(int iterations)
    {
        return this;
    }

    /// <summary>
    ///     Hashes the input string using 32-bit MurmurHash3 with the configured seed.
    /// </summary>
    /// <param name="input">The input to hash.</param>
    /// <returns>A <see cref="Result{T}" /> with a lowercase hex-encoded 32-bit hash, or an error message.</returns>
    public Result<string> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("Murmur3: Input cannot be null or empty.");
            return Result<string>.Failure("Input cannot be null or empty.");
        }

        try
        {
            Logger.Information("Hashing input with MurmurHash3 (32-bit).");
            var buffer = Encoding.UTF8.GetBytes(input);
            var hash = MurmurHash3.Hash32(buffer, _seed);
            return Result<string>.Success(hash.ToString("x8"));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during MurmurHash3 hashing.");
            return Result<string>.Failure($"Error during hashing: {ex.Message}");
        }
    }

    /// <summary>
    ///     Verifies the input by re-hashing and comparing to <paramref name="expectedHash" />.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="expectedHash">A lowercase hex string representing a 32-bit MurmurHash3 value.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public Result Verify(string input, string expectedHash)
    {
        var hashResult = Hash(input);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute Murmur3 hash for verification.");
            return Result.Failure("Failed to compute hash.");
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid ? "MurmurHash3 verification succeeded." : "MurmurHash3 verification failed.");

        return isValid ? Result.Success() : Result.Failure("Verification failed.");
    }

    /// <summary>
    ///     Hashes <paramref name="data" /> with 32-bit MurmurHash3 and returns it Base64-encoded (with the
    ///     <see cref="_seed" />).
    /// </summary>
    /// <param name="data">The byte array to hash.</param>
    /// <returns>A Base64-encoded representation of the 32-bit hash.</returns>
    public Result<string> EncodeToBase64Hash(byte[] data)
    {
        if (data == Array.Empty<byte>() || data.Length is 0)
        {
            Logger.Error("Data cannot be null or empty for Murmur3 encoding.");
            return Result<string>.Failure("Data cannot be null or empty.");
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using MurmurHash3 (32-bit).");
            var hash = MurmurHash3.Hash32(data, _seed);
            var hashBytes = BitConverter.GetBytes(hash);
            var base64Hash = Convert.ToBase64String(hashBytes);

            return Result<string>.Success(base64Hash);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during MurmurHash3 base64 encoding.");
            return Result<string>.Failure($"Error during base64 encoding hash: {ex.Message}");
        }
    }

    /// <summary>
    ///     Verifies <paramref name="data" /> by computing a 32-bit MurmurHash3 and comparing it
    ///     to <paramref name="expectedBase64Hash" />.
    /// </summary>
    /// <param name="data">The original byte array.</param>
    /// <param name="expectedBase64Hash">A Base64-encoded 32-bit MurmurHash3.</param>
    public Result VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        var encodeResult = EncodeToBase64Hash(data);
        if (!encodeResult.IsSuccess)
        {
            Logger.Error("Failed to compute Murmur3 Base64 hash for verification.");
            return Result.Failure("Failed to compute hash.");
        }

        var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
        Logger.Information(isValid
            ? "Murmur3 Base64 hash verification succeeded."
            : "Murmur3 Base64 hash verification failed.");

        return isValid ? Result.Success() : Result.Failure("Base64 hash verification failed.");
    }

    /// <summary>
    ///     No-op for MurmurHash3, as the output size is fixed to 32-bit or 128-bit variants.
    /// </summary>
    public IHasher WithHashSize(int size)
    {
        return this;
    }

    /// <summary>
    ///     Sets the seed value for MurmurHash3.
    /// </summary>
    /// <param name="seed">The new seed.</param>
    /// <returns>The current <see cref="Murmur3Hasher" /> instance.</returns>
    public IHasher WithSeed(uint seed)
    {
        Logger.Information("Setting seed to {Seed} for MurmurHash3.", seed);
        _seed = seed;
        return this;
    }
}
