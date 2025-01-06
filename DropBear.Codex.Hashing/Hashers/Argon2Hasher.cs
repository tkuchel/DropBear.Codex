#region

using System.Collections;
using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Hashing.Helpers;
using DropBear.Codex.Hashing.Interfaces;
using Konscious.Security.Cryptography;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     Provides an Argon2-based <see cref="IHasher" /> implementation for hashing and verifying strings or byte arrays.
///     Supports configurable salt, memory size, degree of parallelism, and iterations.
/// </summary>
public class Argon2Hasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Argon2Hasher>();

    private int _degreeOfParallelism = 8;
    private int _hashSize = 16;
    private int _iterations = 4;
    private int _memorySize = 1024 * 1024; // Default to ~1GB
    private byte[]? _salt;

    /// <summary>
    ///     Sets the salt for Argon2 hashing.
    /// </summary>
    /// <param name="salt">A byte array representing the salt. Must not be null.</param>
    /// <returns>The current <see cref="Argon2Hasher" /> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="salt" /> is null.</exception>
    public IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("Setting salt for Argon2 hashing.");
        _salt = salt ?? throw new ArgumentNullException(nameof(salt), "Salt cannot be null.");
        return this;
    }

    /// <summary>
    ///     Sets the number of iterations for Argon2 hashing.
    /// </summary>
    /// <param name="iterations">The number of iterations (must be at least 1).</param>
    /// <returns>The current <see cref="Argon2Hasher" /> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="iterations" /> is less than 1.</exception>
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

    /// <summary>
    ///     Hashes the given input using Argon2. If no salt is set beforehand, a random salt is generated.
    ///     The resulting hash is returned as a Base64-encoded string, which also includes the salt at the start.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A <see cref="Result{T}" /> containing the Base64-encoded hash, or an error message.</returns>
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
            _salt ??= HashingHelper.GenerateRandomSalt(32); // Generate a 32-byte salt if none is set

            using var argon2 = CreateArgon2(input, _salt);
            var hashBytes = argon2.GetBytes(_hashSize);

            // Combine salt + hash so we can reconstruct the salt on verification
            var combinedBytes = HashingHelper.CombineBytes(_salt, hashBytes);
            Logger.Information("Argon2 hashing successful.");

            return Result<string>.Success(Convert.ToBase64String(combinedBytes));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error computing Argon2 hash.");
            return Result<string>.Failure($"Error computing Argon2 hash: {ex.Message}");
        }
    }

    /// <summary>
    ///     Verifies the given input string against an expected Base64-encoded Argon2 hash string
    ///     that includes the salt at the beginning.
    /// </summary>
    /// <param name="input">The input string to verify.</param>
    /// <param name="expectedHash">A Base64-encoded hash, containing salt + hash bytes.</param>
    /// <returns>A <see cref="Result" /> indicating success if they match, or failure otherwise.</returns>
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

            Logger.Information("Verifying Argon2 hash.");
            var expectedBytes = Convert.FromBase64String(expectedHash);

            // Extract salt + expectedHashBytes from combined array
            var (salt, expectedHashBytes) = HashingHelper.ExtractBytes(expectedBytes, _salt.Length);

            using var argon2 = CreateArgon2(input, salt);
            var hashBytes = argon2.GetBytes(_hashSize);

            // StructuralComparisons checks byte arrays for equality
            var isValid = StructuralComparisons.StructuralEqualityComparer.Equals(hashBytes, expectedHashBytes);
            if (isValid)
            {
                Logger.Information("Argon2 verification successful.");
                return Result.Success();
            }

            Logger.Warning("Argon2 verification failed.");
            return Result.Failure("Verification failed.");
        }
        catch (FormatException ex)
        {
            Logger.Error(ex, "Expected Argon2 hash format is invalid.");
            return Result.Failure("Expected hash format is invalid.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Argon2 verification.");
            return Result.Failure($"Error during Argon2 verification: {ex.Message}");
        }
    }

    /// <summary>
    ///     Encodes the given byte array as a Base64 string (does not perform hashing, for interface compliance).
    /// </summary>
    /// <param name="data">The byte array to encode.</param>
    /// <returns>A <see cref="Result{T}" /> with the Base64-encoded data, or an error message.</returns>
    public Result<string> EncodeToBase64Hash(byte[] data)
    {
        if (data == Array.Empty<byte>() || data.Length == 0)
        {
            Logger.Error("Data cannot be null or empty.");
            return Result<string>.Failure("Data cannot be null or empty.");
        }

        Logger.Information("Encoding data to Base64 hash (no actual Argon2 hashing).");
        return Result<string>.Success(Convert.ToBase64String(data));
    }

    /// <summary>
    ///     Verifies a byte array against a Base64-encoded representation of the array
    ///     (no Argon2 hashing is actually done, used to comply with interface).
    /// </summary>
    /// <param name="data">The original byte array.</param>
    /// <param name="expectedBase64Hash">The Base64-encoded representation to compare against.</param>
    /// <returns>A <see cref="Result" /> indicating success if they match, or failure otherwise.</returns>
    public Result VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        if (data == Array.Empty<byte>() || data.Length == 0)
        {
            Logger.Error("Data cannot be null or empty.");
            return Result.Failure("Data cannot be null or empty.");
        }

        Logger.Information("Verifying Base64 hash (no Argon2 hashing).");
        var base64Hash = Convert.ToBase64String(data);
        return string.Equals(base64Hash, expectedBase64Hash, StringComparison.Ordinal)
            ? Result.Success()
            : Result.Failure("Base64 hash verification failed.");
    }

    /// <summary>
    ///     Sets the hash output size (in bytes) for Argon2 hashing.
    /// </summary>
    /// <param name="size">Number of bytes to output (e.g., 16 or 32).</param>
    /// <returns>The current <see cref="Argon2Hasher" /> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="size" /> is less than 1.</exception>
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

    /// <summary>
    ///     Sets the degree of parallelism (threads) used by Argon2.
    /// </summary>
    /// <param name="degree">Must be at least 1.</param>
    /// <returns>The current <see cref="Argon2Hasher" /> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="degree" /> is less than 1.</exception>
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

    /// <summary>
    ///     Sets the memory size (in kilobytes) for Argon2 hashing.
    /// </summary>
    /// <param name="size">Memory size in KB. Must be at least 1024 (1 MB).</param>
    /// <returns>The current <see cref="Argon2Hasher" /> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="size" /> is less than 1024.</exception>
    public IHasher WithMemorySize(int size)
    {
        if (size < 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Memory size must be at least 1024 KB (1MB).");
        }

        Logger.Information("Setting memory size for Argon2 hashing to {Size} KB.", size);
        _memorySize = size;
        return this;
    }

    /// <summary>
    ///     Creates and configures an Argon2id instance given <paramref name="input" /> and <paramref name="salt" />.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <param name="salt">The salt bytes.</param>
    private Argon2id CreateArgon2(string input, byte[]? salt)
    {
        Logger.Debug(
            "Creating Argon2id instance with Argon2 parameters: Parallelism={Parallel}, Iterations={Iter}, Memory={MemSize}KB",
            _degreeOfParallelism, _iterations, _memorySize);

        return new Argon2id(Encoding.UTF8.GetBytes(input))
        {
            Salt = salt,
            DegreeOfParallelism = _degreeOfParallelism,
            Iterations = _iterations,
            MemorySize = _memorySize
        };
    }
}
