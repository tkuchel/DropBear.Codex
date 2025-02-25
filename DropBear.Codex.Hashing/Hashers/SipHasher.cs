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
///     A <see cref="IHasher" /> implementation using SipHash-2-4 (64-bit).
///     Requires a 16-byte key, no salt or iteration support.
/// </summary>
public sealed class SipHasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<SipHasher>();
    private byte[] _key;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SipHasher" /> class with a 16-byte key.
    /// </summary>
    /// <param name="key">A 16-byte key for SipHash.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key" /> is not 16 bytes.</exception>
    public SipHasher(byte[] key)
    {
        if (key == null || key.Length != 16)
        {
            Logger.Error("SipHash key must be 16 bytes in length.");
            throw new ArgumentException("Key must be 16 bytes in length.", nameof(key));
        }

        _key = key;
    }

    public IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("SipHash does not utilize salt. No-op.");
        return this;
    }

    public IHasher WithIterations(int iterations)
    {
        Logger.Information("SipHash does not utilize iterations. No-op.");
        return this;
    }

    public IHasher WithHashSize(int size)
    {
        Logger.Information("SipHash-2-4 output is fixed at 64 bits. No-op.");
        return this;
    }

    public async Task<Result<string, HashingError>> HashAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("SipHash-2-4: input cannot be null or empty (async).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Logger.Information("Hashing input with SipHash-2-4 (async).");
            var hashHex = await Task.Run(() =>
            {
                var buffer = Encoding.UTF8.GetBytes(input);
                var hashVal = SipHash24.Hash64(buffer, _key);
                return hashVal.ToString("x8");
            }, cancellationToken).ConfigureAwait(false);

            return Result<string, HashingError>.Success(hashHex);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("SipHash hashing was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during SipHash hashing (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    public Result<string, HashingError> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("SipHash-2-4: input cannot be null or empty (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input with SipHash-2-4 (sync).");
            var buffer = Encoding.UTF8.GetBytes(input);
            var hashVal = SipHash24.Hash64(buffer, _key);
            return Result<string, HashingError>.Success(hashVal.ToString("x8"));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during SipHash hashing (sync).");
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
            Logger.Error("Failed to compute SipHash for verification (async).");
            return Result<Unit, HashingError>.Failure(hashResult.Error);
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid
            ? "SipHash verification succeeded (async)."
            : "SipHash verification failed (async).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    public Result<Unit, HashingError> Verify(string input, string expectedHash)
    {
        var hashResult = Hash(input);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute SipHash for verification (sync).");
            return Result<Unit, HashingError>.Failure(hashResult.Error);
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid
            ? "SipHash verification succeeded (sync)."
            : "SipHash verification failed (sync).");

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
            Logger.Error("SipHash: data cannot be null or empty for base64 hash (async).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Logger.Information("Encoding data to Base64 hash using SipHash-2-4 (async).");
            var base64Hash = await Task.Run(() =>
            {
                var hashVal = SipHash24.Hash64(data.Span, _key);
                return Convert.ToBase64String(BitConverter.GetBytes(hashVal));
            }, cancellationToken).ConfigureAwait(false);

            return Result<string, HashingError>.Success(base64Hash);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("SipHash base64 encode was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during SipHash base64 encoding (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    public Result<string, HashingError> EncodeToBase64Hash(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("SipHash: data cannot be null or empty for base64 hash (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using SipHash-2-4 (sync).");
            var hashVal = SipHash24.Hash64(data, _key);
            return Result<string, HashingError>.Success(Convert.ToBase64String(BitConverter.GetBytes(hashVal)));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during SipHash base64 encoding (sync).");
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
            Logger.Error("Failed to compute SipHash base64 hash for verification (async).");
            return Result<Unit, HashingError>.Failure(encodeResult.Error);
        }

        var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
        Logger.Information(isValid
            ? "SipHash base64 verification succeeded (async)."
            : "SipHash base64 verification failed (async).");

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
            Logger.Error("Failed to compute SipHash base64 hash for verification (sync).");
            return Result<Unit, HashingError>.Failure(encodeResult.Error);
        }

        var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
        Logger.Information(isValid
            ? "SipHash base64 verification succeeded (sync)."
            : "SipHash base64 verification failed (sync).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    /// <summary>
    ///     Sets a new 16-byte key for SipHash.
    /// </summary>
    /// <param name="key">A 16-byte key.</param>
    /// <returns>The current <see cref="SipHasher" /> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if the <paramref name="key" /> is not 16 bytes.</exception>
    public IHasher WithKey(byte[] key)
    {
        if (key == null || key.Length != 16)
        {
            Logger.Error("SipHash key must be 16 bytes in length.");
            throw new ArgumentException("Key must be 16 bytes in length.", nameof(key));
        }

        Logger.Information("Setting a new key for SipHasher (SipHash-2-4).");
        _key = key;
        return this;
    }
}
