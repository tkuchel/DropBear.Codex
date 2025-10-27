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
///     A <see cref="IHasher" /> implementation using the XXHash (64-bit) algorithm.
///     Very fast, non-cryptographic hash function. Supports seeding.
/// </summary>
public sealed class XxHasher : BaseHasher
{
    private ulong _seed;
    private bool _use32Bit; // Default to 64-bit

    /// <summary>
    ///     Initializes a new instance of <see cref="XxHasher" /> with an optional seed.
    /// </summary>
    /// <param name="seed">A 64-bit seed for XXHash.</param>
    public XxHasher(ulong seed = 0) : base("XXHash")
    {
        _seed = seed;
    }

    /// <inheritdoc />
    public override IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("XXHash does not support salt. No-op.");
        return this;
    }

    /// <inheritdoc />
    public override IHasher WithIterations(int iterations)
    {
        Logger.Information("XXHash does not support iterations. No-op.");
        return this;
    }

    /// <inheritdoc />
    public override IHasher WithHashSize(int size)
    {
        // XXHash can do 32-bit or 64-bit
        if (size <= 4)
        {
            _use32Bit = true;
            Logger.Information("Using 32-bit XXHash output.");
        }
        else
        {
            _use32Bit = false;
            Logger.Information("Using 64-bit XXHash output.");
        }

        return this;
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

    /// <inheritdoc />
    public override async Task<Result<string, HashingError>> HashAsync(
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
            // XXHash is extremely fast, so only use Task.Run for very large inputs
            if (input.Length > 100000)
            {
                Logger.Information("Hashing input with XXHash ({0}-bit) async with seed {1}.",
                    _use32Bit ? 32 : 64, _seed);

                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var byteCount = Encoding.UTF8.GetByteCount(input);
                    var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
                    try
                    {
                        var bufferSpan = buffer.AsSpan(0, byteCount);
                        Encoding.UTF8.GetBytes(input, bufferSpan);

                        if (_use32Bit)
                        {
                            var hash = XXHash.Hash32(bufferSpan, (uint)_seed);
                            return Result<string, HashingError>.Success(hash.ToString("x8"));
                        }
                        else
                        {
                            var hash = XXHash.Hash64(bufferSpan, _seed);
                            return Result<string, HashingError>.Success(hash.ToString("x16"));
                        }
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

    /// <inheritdoc />
    public override Result<string, HashingError> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("XXHash: Input cannot be null or empty (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input with XXHash ({0}-bit) sync with seed {1}.",
                _use32Bit ? 32 : 64, _seed);

            var byteCount = Encoding.UTF8.GetByteCount(input);

            // Use stackalloc for small inputs, ArrayPool for larger inputs
            if (byteCount <= 512)
            {
                Span<byte> buffer = stackalloc byte[byteCount];
                Encoding.UTF8.GetBytes(input, buffer);

                if (_use32Bit)
                {
                    var hash = XXHash.Hash32(buffer, (uint)_seed);
                    return Result<string, HashingError>.Success(hash.ToString("x8"));
                }
                else
                {
                    var hash = XXHash.Hash64(buffer, _seed);
                    return Result<string, HashingError>.Success(hash.ToString("x16"));
                }
            }
            else
            {
                var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
                try
                {
                    var bufferSpan = buffer.AsSpan(0, byteCount);
                    Encoding.UTF8.GetBytes(input, bufferSpan);

                    if (_use32Bit)
                    {
                        var hash = XXHash.Hash32(bufferSpan, (uint)_seed);
                        return Result<string, HashingError>.Success(hash.ToString("x8"));
                    }
                    else
                    {
                        var hash = XXHash.Hash64(bufferSpan, _seed);
                        return Result<string, HashingError>.Success(hash.ToString("x16"));
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during XXHash hashing (sync).");
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
            Logger.Error("XXHash: Input and expected hash cannot be null or empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Determine if we're working with 32-bit or 64-bit hash based on expected length
        _use32Bit = expectedHash.Length <= 8;

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

    /// <inheritdoc />
    public override Result<Unit, HashingError> Verify(string input, string expectedHash)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
        {
            Logger.Error("XXHash: Input and expected hash cannot be null or empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        // Determine if we're working with 32-bit or 64-bit hash based on expected length
        _use32Bit = expectedHash.Length <= 8;

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

    /// <inheritdoc />
    public override async Task<Result<string, HashingError>> EncodeToBase64HashAsync(
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
            Logger.Information("Encoding data to Base64 hash using XXHash ({0}-bit) async with seed {1}.",
                _use32Bit ? 32 : 64, _seed);

            // Only use Task.Run for large data
            if (data.Length > 100000)
            {
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_use32Bit)
                    {
                        var hash = XXHash.Hash32(data.Span, (uint)_seed);
                        Span<byte> hashBytes = stackalloc byte[4];
                        BitConverter.TryWriteBytes(hashBytes, hash);
                        return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
                    }
                    else
                    {
                        var hash = XXHash.Hash64(data.Span, _seed);
                        Span<byte> hashBytes = stackalloc byte[8];
                        BitConverter.TryWriteBytes(hashBytes, hash);
                        return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
                    }
                }, cancellationToken).ConfigureAwait(false);
            }

            {
                if (_use32Bit)
                {
                    var hash = XXHash.Hash32(data.Span, (uint)_seed);
                    Span<byte> hashBytes = stackalloc byte[4];
                    BitConverter.TryWriteBytes(hashBytes, hash);
                    return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
                }
                else
                {
                    var hash = XXHash.Hash64(data.Span, _seed);
                    Span<byte> hashBytes = stackalloc byte[8];
                    BitConverter.TryWriteBytes(hashBytes, hash);
                    return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
                }
            }
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

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Result<string, HashingError> EncodeToBase64Hash(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("XXHash: data cannot be null or empty for base64 hash (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using XXHash ({0}-bit) sync with seed {1}.",
                _use32Bit ? 32 : 64, _seed);

            if (_use32Bit)
            {
                var hash = XXHash.Hash32(data, (uint)_seed);
                Span<byte> hashBytes = stackalloc byte[4];
                BitConverter.TryWriteBytes(hashBytes, hash);
                return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
            }
            else
            {
                var hash = XXHash.Hash64(data, _seed);
                Span<byte> hashBytes = stackalloc byte[8];
                BitConverter.TryWriteBytes(hashBytes, hash);
                return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during XXHash base64 encoding (sync).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }
}
