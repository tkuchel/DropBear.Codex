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
///     A <see cref="IHasher" /> implementation using the FNV-1a hashing algorithm (32-bit or 64-bit).
///     Simple and fast, but not cryptographically secure. No salt or iterations are used.
/// </summary>
public sealed class Fnv1AHasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Fnv1AHasher>();

    public IHasher WithSalt(byte[]? salt)
    {
        return this;
        // No-op
    }

    public IHasher WithIterations(int iterations)
    {
        return this;
        // No-op
    }

    public IHasher WithHashSize(int size)
    {
        return this;
        // Typically no-op
    }

    /// <inheritdoc />
    public async Task<Result<string, HashingError>> HashAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("FNV-1a: Input cannot be null or empty (async).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Logger.Information("Hashing input with FNV-1a (32-bit) asynchronously.");
            var hashHex = await Task.Run(() =>
            {
                var buffer = Encoding.UTF8.GetBytes(input);
                var hashVal = Fnv1a.Hash32(buffer);
                return hashVal.ToString("x8");
            }, cancellationToken).ConfigureAwait(false);

            return Result<string, HashingError>.Success(hashHex);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("FNV-1a hashing canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during FNV-1a hashing (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public Result<string, HashingError> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("FNV-1a: Input cannot be null or empty (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input with FNV-1a (32-bit) sync.");
            var buffer = Encoding.UTF8.GetBytes(input);
            var hashVal = Fnv1a.Hash32(buffer);
            return Result<string, HashingError>.Success(hashVal.ToString("x8"));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during FNV-1a hashing (sync).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit, HashingError>> VerifyAsync(
        string input,
        string expectedHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hashResult = await HashAsync(input, cancellationToken).ConfigureAwait(false);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute FNV-1a hash for verification (async).");
            return Result<Unit, HashingError>.Failure(hashResult.Error);
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid
            ? "FNV-1a verification succeeded (async)."
            : "FNV-1a verification failed (async).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    /// <inheritdoc />
    public Result<Unit, HashingError> Verify(string input, string expectedHash)
    {
        var hashResult = Hash(input);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute FNV-1a hash for verification (sync).");
            return Result<Unit, HashingError>.Failure(hashResult.Error);
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid
            ? "FNV-1a verification succeeded (sync)."
            : "FNV-1a verification failed (sync).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    /// <inheritdoc />
    public async Task<Result<string, HashingError>> EncodeToBase64HashAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        if (data.IsEmpty)
        {
            Logger.Error("FNV-1a: Data cannot be null or empty for base64 hash (async).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Logger.Information("Encoding data to Base64 with FNV-1a (64-bit) async.");
            var base64Hash = await Task.Run(() =>
            {
                var hashVal = Fnv1a.Hash64(data.Span);
                return Convert.ToBase64String(BitConverter.GetBytes(hashVal));
            }, cancellationToken).ConfigureAwait(false);

            return Result<string, HashingError>.Success(base64Hash);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("FNV-1a base64 encode was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during FNV-1a base64 encoding (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public Result<string, HashingError> EncodeToBase64Hash(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("FNV-1a: Data cannot be null or empty for base64 hash (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Encoding data to Base64 with FNV-1a (64-bit) sync.");
            var hashVal = Fnv1a.Hash64(data);
            var base64Hash = Convert.ToBase64String(BitConverter.GetBytes(hashVal));

            return Result<string, HashingError>.Success(base64Hash);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during FNV-1a base64 encoding (sync).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <inheritdoc />
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
            Logger.Error("Failed to compute FNV-1a Base64 hash for verification (async).");
            return Result<Unit, HashingError>.Failure(encodeResult.Error);
        }

        var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
        Logger.Information(isValid
            ? "FNV-1a Base64 hash verification succeeded (async)."
            : "FNV-1a Base64 hash verification failed (async).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    /// <inheritdoc />
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
            ? "FNV-1a Base64 hash verification succeeded (sync)."
            : "FNV-1a Base64 hash verification failed (sync).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }
}
