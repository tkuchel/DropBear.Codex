#region

using System.Text;
using Blake3;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Hashing.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     A <see cref="IHasher" /> implementation using the Blake3 algorithm (fixed output size).
///     Does not support salt or iterations, methods are included for interface compliance.
/// </summary>
public class Blake3Hasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Blake3Hasher>();

    /// <inheritdoc />
    public IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("WithSalt called, but Blake3 does not use salt.");
        return this;
    }

    /// <inheritdoc />
    public IHasher WithIterations(int iterations)
    {
        Logger.Information("WithIterations called, but Blake3 does not use iterations.");
        return this;
    }

    /// <inheritdoc />
    public Result<string> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("Blake3: Input cannot be null or empty.");
            return Result<string>.Failure("Input cannot be null or empty.");
        }

        try
        {
            Logger.Information("Hashing input using Blake3.");
            var hash = Hasher.Hash(Encoding.UTF8.GetBytes(input)).ToString();
            Logger.Information("Blake3 hashing successful.");
            return Result<string>.Success(hash);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 hashing.");
            return Result<string>.Failure($"Error during hashing: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result Verify(string input, string expectedHash)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
        {
            Logger.Error("Input and expected hash cannot be null or empty for Blake3 verification.");
            return Result.Failure("Input and expected hash cannot be null or empty.");
        }

        try
        {
            Logger.Information("Verifying hash using Blake3.");
            var hash = Hasher.Hash(Encoding.UTF8.GetBytes(input)).ToString();

            return string.Equals(hash, expectedHash, StringComparison.Ordinal)
                ? Result.Success()
                : Result.Failure("Verification failed.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 verification.");
            return Result.Failure($"Error during verification: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result<string> EncodeToBase64Hash(byte[] data)
    {
        if (data == Array.Empty<byte>() || data.Length == 0)
        {
            Logger.Error("Data cannot be null or empty for Blake3 encoding.");
            return Result<string>.Failure("Data cannot be null or empty.");
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using Blake3.");
            var hash = Hasher.Hash(data);
            return Result<string>.Success(Convert.ToBase64String(hash.AsSpan()));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 Base64 encoding hash.");
            return Result<string>.Failure($"Error during base64 hash: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        if (data == Array.Empty<byte>() || data.Length == 0)
        {
            Logger.Error("Data cannot be null or empty for Blake3 verification.");
            return Result.Failure("Data cannot be null or empty.");
        }

        try
        {
            Logger.Information("Verifying Base64 hash using Blake3.");
            var hash = Hasher.Hash(data);
            var base64Hash = Convert.ToBase64String(hash.AsSpan());

            return string.Equals(base64Hash, expectedBase64Hash, StringComparison.Ordinal)
                ? Result.Success()
                : Result.Failure("Base64 hash verification failed.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 Base64 hash verification.");
            return Result.Failure($"Error during base64 hash verification: {ex.Message}");
        }
    }


    public IHasher WithHashSize(int size)
    {
        // Blake3 has a fixed or variable output, but we treat it as fixed for interface compliance
        Logger.Information("WithHashSize called, but Blake3 can have a variable output. No-op here.");
        return this;
    }
}
