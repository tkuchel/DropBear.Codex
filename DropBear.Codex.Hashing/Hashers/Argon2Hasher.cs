#region

using System.Collections;
using System.Text;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Hashing.Helpers;
using DropBear.Codex.Hashing.Interfaces;
using Konscious.Security.Cryptography;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

public class Argon2Hasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Argon2Hasher>();

    private int _degreeOfParallelism = 8;
    private int _hashSize = 16;
    private int _iterations = 4;
    private int _memorySize = 1024 * 1024; // 1GB
    private byte[]? _salt;

    public IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("Setting salt for Argon2 hashing.");
        _salt = salt ?? throw new ArgumentNullException(nameof(salt), "Salt cannot be null.");
        return this;
    }

    public IHasher WithIterations(int iterations)
    {
        if (iterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be at least 1.");
        }

        Logger.Information("Setting iterations for Argon2 hashing to {Iterations}.", iterations);
        _iterations = iterations;
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
            Logger.Information("Hashing input using Argon2.");
            _salt ??= HashingHelper.GenerateRandomSalt(32);
            using var argon2 = CreateArgon2(input, _salt);
            var hashBytes = argon2.GetBytes(_hashSize);
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
        try
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

            Logger.Information("Verifying hash using Argon2.");
            var expectedBytes = Convert.FromBase64String(expectedHash);
            var (salt, expectedHashBytes) = HashingHelper.ExtractBytes(expectedBytes, _salt.Length);
            using var argon2 = CreateArgon2(input, salt);
            var hashBytes = argon2.GetBytes(_hashSize);

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

        Logger.Information("Encoding data to Base64 hash.");
        return Result<string>.Success(Convert.ToBase64String(data));
    }

    public Result VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        if (data == Array.Empty<byte>() || data.Length == 0)
        {
            Logger.Error("Data cannot be null or empty.");
            return Result.Failure("Data cannot be null or empty.");
        }

        Logger.Information("Verifying Base64 hash.");
        var base64Hash = Convert.ToBase64String(data);
        return string.Equals(base64Hash, expectedBase64Hash, StringComparison.Ordinal)
            ? Result.Success()
            : Result.Failure("Base64 hash verification failed.");
    }

    public IHasher WithHashSize(int size)
    {
        if (size < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Hash size must be at least 1 byte.");
        }

        Logger.Information("Setting hash size for Argon2 hashing to {Size} bytes.", size);
        _hashSize = size;
        return this;
    }

    public IHasher WithDegreeOfParallelism(int degree)
    {
        if (degree < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(degree), "Degree of parallelism must be at least 1.");
        }

        Logger.Information("Setting degree of parallelism for Argon2 hashing to {Degree}.", degree);
        _degreeOfParallelism = degree;
        return this;
    }

    public IHasher WithMemorySize(int size)
    {
        if (size < 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Memory size must be at least 1024 bytes (1KB).");
        }

        Logger.Information("Setting memory size for Argon2 hashing to {Size} bytes.", size);
        _memorySize = size;
        return this;
    }

    private Argon2id CreateArgon2(string input, byte[]? salt)
    {
        Logger.Debug("Creating Argon2id instance with provided parameters.");
        return new Argon2id(Encoding.UTF8.GetBytes(input))
        {
            Salt = salt,
            DegreeOfParallelism = _degreeOfParallelism,
            Iterations = _iterations,
            MemorySize = _memorySize
        };
    }
}
