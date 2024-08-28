#region

using System.Collections;
using System.Text;
using Blake2Fast;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Hashing.Helpers;
using DropBear.Codex.Hashing.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

public class Blake2Hasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Blake2Hasher>();

    private int _hashSize = 32; // Default hash size for Blake2b
    private byte[]? _salt;

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

    public IHasher WithIterations(int iterations)
    {
        // Blake2 does not use iterations, so this method is a no-op.
        Logger.Information("WithIterations called but not applicable for Blake2Hasher.");
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
            Logger.Information("Hashing input using Blake2.");
            _salt ??= HashingHelper.GenerateRandomSalt(32);
            var hashBytes = HashWithBlake2(input, _salt);
            var combinedBytes = HashingHelper.CombineBytes(_salt, hashBytes);
            Logger.Information("Hashing successful.");
            return Result<string>.Success(Convert.ToBase64String(combinedBytes));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error computing hash.");
            return Result<string>.Failure($"Error computing hash: {ex.Message}");
        }
    }

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
            Logger.Information("Verifying hash using Blake2.");
            var expectedBytes = Convert.FromBase64String(expectedHash);
            var (salt, expectedHashBytes) = HashingHelper.ExtractBytes(expectedBytes, _salt.Length);
            var hashBytes = HashWithBlake2(input, salt);

            var isValid = StructuralComparisons.StructuralEqualityComparer.Equals(hashBytes, expectedHashBytes);
            if (isValid)
            {
                Logger.Information("Verification successful.");
                return Result.Success();
            }

            Logger.Warning("Verification failed.");
            return Result.Failure("Verification failed.");
        }
        catch (FormatException ex)
        {
            Logger.Error(ex, "Expected hash format is invalid.");
            return Result.Failure("Expected hash format is invalid.");
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

        Logger.Information("Encoding data to Base64 hash using Blake2.");
        var hash = Blake2b.ComputeHash(_hashSize, data);
        return Result<string>.Success(Convert.ToBase64String(hash));
    }

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

        return base64Hash == expectedBase64Hash ? Result.Success() : Result.Failure("Base64 hash verification failed.");
    }

    public IHasher WithHashSize(int size)
    {
        if (size < 1)
        {
            Logger.Error("Hash size must be at least 1 byte.");
            throw new ArgumentOutOfRangeException(nameof(size), "Hash size must be at least 1 byte.");
        }

        Logger.Information("Setting hash size for Blake2 hashing to {Size} bytes.", size);
        _hashSize = size;
        return this;
    }

    private byte[] HashWithBlake2(string input, byte[]? salt)
    {
        Logger.Debug("Hashing input using Blake2 with provided salt.");
        var inputBytes = Encoding.UTF8.GetBytes(input);
        if (salt != null)
        {
            var saltedInput = HashingHelper.CombineBytes(salt, inputBytes);
            return Blake2b.ComputeHash(_hashSize, saltedInput);
        }

        return Blake2b.ComputeHash(_hashSize, inputBytes);
    }
}
