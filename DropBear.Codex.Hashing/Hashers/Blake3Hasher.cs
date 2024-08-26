#region

using System.Text;
using Blake3;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Hashing.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

public class Blake3Hasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Blake3Hasher>();

    // Blake3 does not use salt or iterations, so related methods are no-ops but included for interface compatibility
    public IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("WithSalt called, but Blake3 does not use salt.");
        return this;
    }

    public IHasher WithIterations(int iterations)
    {
        Logger.Information("WithIterations called, but Blake3 does not use iterations.");
        return this;
    }

    public Result<string> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("Input cannot be null or empty.");
            return Result<string>.Failure("Input cannot be null or empty.");
        }

        try
        {
            Logger.Information("Hashing input using Blake3.");
            var hash = Hasher.Hash(Encoding.UTF8.GetBytes(input)).ToString();
            Logger.Information("Hashing successful.");
            return Result<string>.Success(hash);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during hashing.");
            return Result<string>.Failure($"Error during hashing: {ex.Message}");
        }
    }

    public Result Verify(string input, string expectedHash)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
        {
            Logger.Error("Input and expected hash cannot be null or empty.");
            return Result.Failure("Input and expected hash cannot be null or empty.");
        }

        try
        {
            Logger.Information("Verifying hash using Blake3.");
            var hash = Hasher.Hash(Encoding.UTF8.GetBytes(input)).ToString();
            return hash == expectedHash ? Result.Success() : Result.Failure("Verification failed.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during verification.");
            return Result.Failure($"Error during verification: {ex.Message}");
        }
    }

    public Result<string> EncodeToBase64Hash(byte[] data)
    {
        if (data == Array.Empty<byte>() || data.Length == 0)
        {
            Logger.Error("Data cannot be null or empty.");
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
            Logger.Error(ex, "Error during Base64 encoding hash.");
            return Result<string>.Failure($"Error during base64 encoding hash: {ex.Message}");
        }
    }

    public Result VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        if (data == Array.Empty<byte>() || data.Length == 0)
        {
            Logger.Error("Data cannot be null or empty.");
            return Result.Failure("Data cannot be null or empty.");
        }

        try
        {
            Logger.Information("Verifying Base64 hash using Blake3.");
            var hash = Hasher.Hash(data);
            var base64Hash = Convert.ToBase64String(hash.AsSpan());
            return base64Hash == expectedBase64Hash
                ? Result.Success()
                : Result.Failure("Base64 hash verification failed.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Base64 hash verification.");
            return Result.Failure($"Error during base64 hash verification: {ex.Message}");
        }
    }

#pragma warning disable IDE0060 // Remove unused parameter
    // Blake3 has a fixed output size but implementing to comply with interface.
    public IHasher WithHashSize(int size)
    {
        Logger.Information("WithHashSize called, but Blake3 has a fixed output size.");
        return this;
    }
#pragma warning restore IDE0060 // Remove unused parameter
}
