#region

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Hashing.Errors;
using DropBear.Codex.Hashing.Interfaces;
using HashDepot;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     A <see cref="IHasher" /> implementation using the MurmurHash3 algorithm (32-bit).
///     Optimized for good distribution and speed. Supports optional seeding.
///     Does not support salt or iterations.
/// </summary>
public sealed class Murmur3Hasher : BaseHasher
{
    private uint _seed;

    /// <summary>
    ///     Initializes a new instance of <see cref="Murmur3Hasher" />.
    /// </summary>
    /// <param name="seed">Optional seed value for the hash calculation.</param>
    public Murmur3Hasher(uint seed = 0) : base("MurmurHash3")
    {
        _seed = seed;
    }

    /// <inheritdoc />
    public override IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("MurmurHash3 does not support salt. No-op.");
        return this;
    }

    /// <inheritdoc />
    public override IHasher WithIterations(int iterations)
    {
        Logger.Information("MurmurHash3 does not support iterations. No-op.");
        return this;
    }

    /// <inheritdoc />
    public override IHasher WithHashSize(int size)
    {
        Logger.Information("MurmurHash3 output size is fixed at 32-bit. No-op.");
        return this;
    }

    /// <summary>
    ///     Configures the seed for the MurmurHash3 algorithm.
    /// </summary>
    /// <param name="seed">The seed value to use.</param>
    /// <returns>The current <see cref="Murmur3Hasher" /> instance for chaining.</returns>
    public IHasher WithSeed(uint seed)
    {
        Logger.Information("Setting seed for MurmurHash3 to {Seed}.", seed);
        _seed = seed;
        return this;
    }

    /// <inheritdoc />
    public override async Task<Result<string, HashingError>> HashAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("MurmurHash3: input cannot be null or empty (async).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // MurmurHash3 is fast enough that we only need to offload to a separate thread for large inputs
            if (input.Length > 10000)
            {
                Logger.Information("Hashing input with MurmurHash3 (32-bit) async with seed {Seed}.", _seed);

                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var byteCount = Encoding.UTF8.GetByteCount(input);
                    var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
                    try
                    {
                        var bufferSpan = buffer.AsSpan(0, byteCount);
                        Encoding.UTF8.GetBytes(input, bufferSpan);
                        return ComputeHashHex(bufferSpan);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }, cancellationToken).ConfigureAwait(false);
            }

            // For small inputs, just use the synchronous method
            return Hash(input);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("MurmurHash3 hashing was canceled (async).");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during MurmurHash3 hashing (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public override Result<string, HashingError> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("MurmurHash3: input cannot be null or empty (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input with MurmurHash3 (32-bit) sync with seed {Seed}.", _seed);
            var byteCount = Encoding.UTF8.GetByteCount(input);

            // Use stackalloc for small inputs, ArrayPool for larger inputs
            if (byteCount <= 512)
            {
                Span<byte> buffer = stackalloc byte[byteCount];
                Encoding.UTF8.GetBytes(input, buffer);
                return ComputeHashHex(buffer);
            }
            else
            {
                var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
                try
                {
                    var bufferSpan = buffer.AsSpan(0, byteCount);
                    Encoding.UTF8.GetBytes(input, bufferSpan);
                    return ComputeHashHex(bufferSpan);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during MurmurHash3 hashing (sync).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public override async Task<Result<Unit, HashingError>> VerifyAsync(
        string input,
        string expectedHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
        {
            Logger.Error("MurmurHash3: Input and expected hash cannot be null or empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var hashResult = await HashAsync(input, cancellationToken).ConfigureAwait(false);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute MurmurHash3 hash for verification (async).");
            return Result<Unit, HashingError>.Failure(hashResult.Error);
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid
            ? "MurmurHash3 verification succeeded (async)."
            : "MurmurHash3 verification failed (async).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    /// <inheritdoc />
    public override Result<Unit, HashingError> Verify(string input, string expectedHash)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
        {
            Logger.Error("MurmurHash3: Input and expected hash cannot be null or empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        var hashResult = Hash(input);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute MurmurHash3 hash for verification (sync).");
            return Result<Unit, HashingError>.Failure(hashResult.Error);
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid
            ? "MurmurHash3 verification succeeded (sync)."
            : "MurmurHash3 verification failed (sync).");

        return isValid
            ? Result<Unit, HashingError>.Success(Unit.Value)
            : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
    }

    /// <inheritdoc />
    public override async Task<Result<string, HashingError>> EncodeToBase64HashAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        if (data.IsEmpty)
        {
            Logger.Error("MurmurHash3: Data cannot be null or empty (base64 encode async).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Logger.Information("Encoding data to Base64 hash using MurmurHash3 with seed {Seed} (async).", _seed);

            // Only use Task.Run for larger data
            if (data.Length > 10000)
            {
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var hash = MurmurHash3.Hash32(data.Span, _seed);
                    Span<byte> hashBytes = stackalloc byte[4];
                    BitConverter.TryWriteBytes(hashBytes, hash);
                    return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
                }, cancellationToken).ConfigureAwait(false);
            }

            {
                var hash = MurmurHash3.Hash32(data.Span, _seed);
                Span<byte> hashBytes = stackalloc byte[4];
                BitConverter.TryWriteBytes(hashBytes, hash);
                return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Information("MurmurHash3 base64 encode canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during MurmurHash3 base64 encoding (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Result<string, HashingError> EncodeToBase64Hash(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("MurmurHash3: Data cannot be null or empty (base64 encode sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using MurmurHash3 with seed {Seed} (sync).", _seed);
            var hash = MurmurHash3.Hash32(data, _seed);
            Span<byte> hashBytes = stackalloc byte[4];
            BitConverter.TryWriteBytes(hashBytes, hash);
            return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during MurmurHash3 base64 encoding (sync).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <summary>
    ///     Computes the MurmurHash3 hash of the input data and returns it as a hex string.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>A result containing a hex string representation of the hash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Result<string, HashingError> ComputeHashHex(ReadOnlySpan<byte> data)
    {
        var hashVal = MurmurHash3.Hash32(data, _seed);
        return Result<string, HashingError>.Success(hashVal.ToString("x8"));
    }

    /// <summary>
    ///     Computes the MurmurHash3 hash of the input data and returns it as a Base64 string.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>A result containing a Base64 string representation of the hash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Result<string, HashingError> ComputeHashBase64(ReadOnlySpan<byte> data)
    {
        var hashVal = MurmurHash3.Hash32(data, _seed);
        Span<byte> hashBytes = stackalloc byte[4];
        BitConverter.TryWriteBytes(hashBytes, hashVal);
        return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
    }
}
