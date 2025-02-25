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
///     A <see cref="IHasher" /> implementation using the XXHash (64-bit) algorithm.
///     Does not support salt or iterations; has optional seed usage.
/// </summary>
public sealed class XxHasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<XxHasher>();
    private ulong _seed;

    /// <summary>
    ///     Initializes a new instance of <see cref="XxHasher" /> with an optional 64-bit seed.
    /// </summary>
    /// <param name="seed">A 64-bit seed for XXHash.</param>
    public XxHasher(ulong seed = 0)
    {
        _seed = seed;
    }

    public IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("XXHash does not support salt. No-op.");
        return this;
    }

    public IHasher WithIterations(int iterations)
    {
        Logger.Information("XXHash does not support iterations. No-op.");
        return this;
    }

    public IHasher WithHashSize(int size)
    {
        Logger.Information("XXHash is typically 32/64-bit. We are using 64-bit. No-op for interface compliance.");
        return this;
    }

    public async Task<Result<string, HashingError>> HashAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("XXHash: Input cannot be null or empty (async).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Logger.Information("Hashing input with XXHash (64-bit) async.");
            var hashHex = await Task.Run(() =>
            {
                var buffer = Encoding.UTF8.GetBytes(input);
                var hashVal = XXHash.Hash64(buffer, _seed);
                return hashVal.ToString("x8");
            }, cancellationToken).ConfigureAwait(false);

            return Result<string, HashingError>.Success(hashHex);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("XXHash hashing was canceled (async).");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during XXHash hashing (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    public Result<string, HashingError> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("XXHash: Input cannot be null or empty (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input with XXHash (64-bit) sync.");
            var buffer = Encoding.UTF8.GetBytes(input);
            var hashVal = XXHash.Hash64(buffer, _seed);
            return Result<string, HashingError>.Success(hashVal.ToString("x8"));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during XXHash hashing (sync).");
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
            Logger.Error("Failed to compute XXHash for verification (async).");
            return Result<Unit, HashingError>.Failure(hashResult.Error);
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid
            ? "XXHash verification succeeded (async)."
            : "XXHash verification failed (async).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    public Result<Unit, HashingError> Verify(string input, string expectedHash)
    {
        var hashResult = Hash(input);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute XXHash for verification (sync).");
            return Result<Unit, HashingError>.Failure(hashResult.Error);
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid
            ? "XXHash verification succeeded (sync)."
            : "XXHash verification failed (sync).");

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
            Logger.Error("XXHash: data cannot be null or empty for base64 hash (async).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Logger.Information("Encoding data to Base64 hash using XXHash (64-bit) async.");
            var base64Hash = await Task.Run(() =>
            {
                var hashVal = XXHash.Hash64(data.Span, _seed);
                var hashBytes = BitConverter.GetBytes(hashVal);
                return Convert.ToBase64String(hashBytes);
            }, cancellationToken).ConfigureAwait(false);

            return Result<string, HashingError>.Success(base64Hash);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("XXHash base64 encode was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during XXHash base64 encoding (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    public Result<string, HashingError> EncodeToBase64Hash(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("XXHash: data cannot be null or empty for base64 hash (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using XXHash (64-bit) sync.");
            var hashVal = XXHash.Hash64(data, _seed);
            var hashBytes = BitConverter.GetBytes(hashVal);
            return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during XXHash base64 encoding (sync).");
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
            Logger.Error("Failed to compute XXHash base64 hash for verification (async).");
            return Result<Unit, HashingError>.Failure(encodeResult.Error);
        }

        var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
        Logger.Information(isValid
            ? "XXHash base64 verification succeeded (async)."
            : "XXHash base64 verification failed (async).");

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
            Logger.Error("Failed to compute XXHash base64 hash for verification (sync).");
            return Result<Unit, HashingError>.Failure(encodeResult.Error);
        }

        var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
        Logger.Information(isValid
            ? "XXHash base64 verification succeeded (sync)."
            : "XXHash base64 verification failed (sync).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    /// <summary>
    ///     Sets the seed for XXHash operations.
    /// </summary>
    /// <param name="seed">A 64-bit seed.</param>
    /// <returns>The current <see cref="XxHasher" /> instance.</returns>
    public IHasher WithSeed(ulong seed)
    {
        Logger.Information("Setting seed for XxHasher to {Seed}.", seed);
        _seed = seed;
        return this;
    }
}
