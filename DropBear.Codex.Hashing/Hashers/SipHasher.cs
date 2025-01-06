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
///     A <see cref="IHasher" /> implementation using SipHash-2-4 (64-bit).
///     Requires a 16-byte key.
/// </summary>
public sealed class SipHasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<SipHasher>();
    private byte[] _key;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SipHasher" /> class.
    /// </summary>
    /// <param name="key">A 16-byte key for SipHash.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key" /> is null or not 16 bytes long.</exception>
    public SipHasher(byte[] key)
    {
        if (key == Array.Empty<byte>() || key.Length is not 16)
        {
            Logger.Error("Key must be 16 bytes in length for SipHash.");
            throw new ArgumentException("Key must be 16 bytes in length.", nameof(key));
        }

        _key = key;
    }

    /// <summary>
    ///     SipHash does not utilize salt, so this is a no-op.
    /// </summary>
    public IHasher WithSalt(byte[]? salt)
    {
        return this;
    }

    /// <summary>
    ///     SipHash does not utilize iterations, so this is a no-op.
    /// </summary>
    public IHasher WithIterations(int iterations)
    {
        return this;
    }

    /// <summary>
    ///     Hashes the input string using SipHash-2-4 with the current 16-byte key.
    /// </summary>
    /// <param name="input">The string to hash.</param>
    /// <returns>A hex-encoded 64-bit hash.</returns>
    public Result<string> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("SipHash: Input cannot be null or empty.");
            return Result<string>.Failure("Input cannot be null or empty.");
        }

        try
        {
            Logger.Information("Hashing input with SipHash-2-4.");
            var buffer = Encoding.UTF8.GetBytes(input);
            var hash = SipHash24.Hash64(buffer, _key);
            return Result<string>.Success(hash.ToString("x8"));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during SipHash hashing.");
            return Result<string>.Failure($"Error during hashing: {ex.Message}");
        }
    }

    /// <summary>
    ///     Verifies the given input against a hex-encoded SipHash-2-4 output.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="expectedHash">A hex-encoded 64-bit hash.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public Result Verify(string input, string expectedHash)
    {
        var hashResult = Hash(input);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute SipHash for verification.");
            return Result.Failure("Failed to compute hash.");
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid ? "SipHash verification succeeded." : "SipHash verification failed.");

        return isValid ? Result.Success() : Result.Failure("Verification failed.");
    }

    /// <summary>
    ///     Computes a SipHash-2-4 for the byte array, returning a Base64-encoded representation of the 64-bit output.
    /// </summary>
    /// <param name="data">The byte array to hash.</param>
    /// <returns>A Base64-encoded SipHash output.</returns>
    public Result<string> EncodeToBase64Hash(byte[] data)
    {
        if (data == Array.Empty<byte>() || data.Length is 0)
        {
            Logger.Error("SipHash: Data cannot be null or empty.");
            return Result<string>.Failure("Data cannot be null or empty.");
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using SipHash-2-4.");
            var hash = SipHash24.Hash64(data, _key);
            var hashBytes = BitConverter.GetBytes(hash);
            var base64Hash = Convert.ToBase64String(hashBytes);

            return Result<string>.Success(base64Hash);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during SipHash base64 encoding.");
            return Result<string>.Failure($"Error during base64 encoding hash: {ex.Message}");
        }
    }

    /// <summary>
    ///     Verifies <paramref name="data" /> by computing a SipHash-2-4 and comparing it to
    ///     <paramref name="expectedBase64Hash" />.
    /// </summary>
    /// <param name="data">The byte array to hash.</param>
    /// <param name="expectedBase64Hash">A Base64-encoded SipHash-2-4 output.</param>
    /// <returns>A <see cref="Result" /> indicating success if they match.</returns>
    public Result VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        var encodeResult = EncodeToBase64Hash(data);
        if (!encodeResult.IsSuccess)
        {
            Logger.Error("Failed to compute SipHash base64 hash for verification.");
            return Result.Failure("Failed to compute hash.");
        }

        var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
        Logger.Information(isValid ? "SipHash base64 verification succeeded." : "SipHash base64 verification failed.");

        return isValid ? Result.Success() : Result.Failure("Base64 hash verification failed.");
    }

    /// <summary>
    ///     Sets a new 16-byte key for SipHash.
    /// </summary>
    /// <param name="key">A 16-byte key.</param>
    /// <returns>The current <see cref="SipHasher" /> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if the <paramref name="key" /> is not 16 bytes.</exception>
    public IHasher WithKey(byte[] key)
    {
        if (key == Array.Empty<byte>() || key.Length is not 16)
        {
            Logger.Error("SipHash key must be 16 bytes in length.");
            throw new ArgumentException("Key must be 16 bytes in length.", nameof(key));
        }

        Logger.Information("Setting a new key for SipHasher (SipHash-2-4).");
        _key = key;
        return this;
    }

    /// <summary>
    ///     No-op for SipHash. SipHash-2-4 output is fixed at 64 bits.
    /// </summary>
    public IHasher WithHashSize(int size)
    {
        return this;
    }
}
