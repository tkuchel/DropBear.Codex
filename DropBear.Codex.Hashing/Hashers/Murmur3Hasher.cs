#region

using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Hashing.Errors;
using DropBear.Codex.Hashing.Interfaces;
using HashDepot;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     A <see cref="IHasher" /> implementation using the MurmurHash3 algorithm (32-bit).
///     Does not support salt or iterations; logs no-ops for them.
/// </summary>
public sealed class Murmur3Hasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Murmur3Hasher>();
    private uint _seed;

    public Murmur3Hasher(uint seed = 0)
    {
        _seed = seed;
    }

    public IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("Murmur3 does not support salt. No-op.");
        return this;
    }

    public IHasher WithIterations(int iterations)
    {
        Logger.Information("Murmur3 does not support iterations. No-op.");
        return this;
    }

    public IHasher WithHashSize(int size)
    {
        Logger.Information("Murmur3 typically 32-bit or 128. We do 32-bit. No-op for interface compliance.");
        return this;
    }

    public async Task<Result<string, HashingError>> HashAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("Murmur3: input cannot be null or empty (async).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Logger.Information("Hashing input with MurmurHash3 (32-bit) async.");
            var hashHex = await Task.Run(() =>
            {
                var buffer = Encoding.UTF8.GetBytes(input);
                var hash = MurmurHash3.Hash32(buffer, _seed);
                return hash.ToString("x8");
            }, cancellationToken).ConfigureAwait(false);

            return Result<string, HashingError>.Success(hashHex);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Murmur3 hashing was canceled (async).");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during MurmurHash3 hashing (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    public Result<string, HashingError> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("Murmur3: input cannot be null or empty (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input with MurmurHash3 (32-bit) sync.");
            var buffer = Encoding.UTF8.GetBytes(input);
            var hash = MurmurHash3.Hash32(buffer, _seed);
            return Result<string, HashingError>.Success(hash.ToString("x8"));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during MurmurHash3 hashing (sync).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    public async Task<Result<Unit, HashingError>> VerifyAsync(
        string input,
        string expectedHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hashResult = await HashAsync(input, cancellationToken).ConfigureAwait(false);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute Murmur3 hash for verification (async).");
            return Result<Unit, HashingError>.Failure(hashResult.Error);
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid
            ? "Murmur3 verification succeeded (async)."
            : "Murmur3 verification failed (async).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    public Result<Unit, HashingError> Verify(string input, string expectedHash)
    {
        var hashResult = Hash(input);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute Murmur3 hash for verification (sync).");
            return Result<Unit, HashingError>.Failure(hashResult.Error);
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid
            ? "Murmur3 verification succeeded (sync)."
            : "Murmur3 verification failed (sync).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    public async Task<Result<string, HashingError>> EncodeToBase64HashAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        if (data.IsEmpty)
        {
            Logger.Error("Murmur3: Data cannot be null or empty (base64 encode async).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Logger.Information("Encoding data to Base64 hash using MurmurHash3 (32-bit) async.");
            var base64Result = await Task.Run(() =>
            {
                var hash = MurmurHash3.Hash32(data.Span, _seed);
                var hashBytes = BitConverter.GetBytes(hash);
                return Convert.ToBase64String(hashBytes);
            }, cancellationToken).ConfigureAwait(false);

            return Result<string, HashingError>.Success(base64Result);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Murmur3 base64 encode canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Murmur3 base64 encoding (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    public Result<string, HashingError> EncodeToBase64Hash(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("Murmur3: Data cannot be null or empty (base64 encode sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using MurmurHash3 (32-bit) sync.");
            var hash = MurmurHash3.Hash32(data, _seed);
            var hashBytes = BitConverter.GetBytes(hash);
            return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Murmur3 base64 encoding (sync).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    public async Task<Result<Unit, HashingError>> VerifyBase64HashAsync(
        ReadOnlyMemory<byte> data,
        string expectedBase64Hash,
        CancellationToken cancellationToken = default)
    {
        if (data.IsEmpty)
        {
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var encodeResult = await EncodeToBase64HashAsync(data, cancellationToken).ConfigureAwait(false);
        if (!encodeResult.IsSuccess)
        {
            Logger.Error("Failed to compute Murmur3 Base64 hash for verification (async).");
            return Result<Unit, HashingError>.Failure(encodeResult.Error);
        }

        var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
        Logger.Information(isValid
            ? "Murmur3 Base64 hash verification succeeded (async)."
            : "Murmur3 Base64 hash verification failed (async).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    public Result<Unit, HashingError> VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        if (data == null || data.Length == 0)
        {
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        var encodeResult = EncodeToBase64Hash(data);
        if (!encodeResult.IsSuccess)
        {
            return Result<Unit, HashingError>.Failure(encodeResult.Error);
        }

        var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
        Logger.Information(isValid
            ? "Murmur3 Base64 hash verification succeeded (sync)."
            : "Murmur3 Base64 hash verification failed (sync).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    /// <summary>
    ///     Configures the seed for the MurmurHash3 algorithm.
    /// </summary>
    public IHasher WithSeed(uint seed)
    {
        Logger.Information("Setting seed for Murmur3Hasher to {Seed}.", seed);
        _seed = seed;
        return this;
    }
}
