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
///     A <see cref="IHasher" /> implementation using the FNV-1a hashing algorithm (32-bit or 64-bit).
///     Simple and fast, but not cryptographically secure. No salt or iterations are used.
/// </summary>
public sealed class Fnv1AHasher : BaseHasher
{
    private bool _use64Bit; // Default to 32-bit

    /// <summary>
    ///     Initializes a new instance of <see cref="Fnv1AHasher" />.
    /// </summary>
    public Fnv1AHasher() : base("FNV-1a")
    {
    }

    /// <inheritdoc />
    public override IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("FNV-1a does not support salt. No-op.");
        return this;
    }

    /// <inheritdoc />
    public override Result<IHasher, HashingError> WithSaltValidated(byte[]? salt)
    {
        Logger.Information("FNV-1a does not support salt. No-op.");
        return Result<IHasher, HashingError>.Success(this);
    }

    /// <inheritdoc />
    public override IHasher WithIterations(int iterations)
    {
        Logger.Information("FNV-1a does not support iterations. No-op.");
        return this;
    }

    /// <inheritdoc />
    public override Result<IHasher, HashingError> WithIterationsValidated(int iterations)
    {
        Logger.Information("FNV-1a does not support iterations. No-op.");
        return Result<IHasher, HashingError>.Success(this);
    }

    /// <inheritdoc />
    public override IHasher WithHashSize(int size)
    {
        // For FNV-1a, we support two sizes: 4 bytes (32-bit) and 8 bytes (64-bit)
        if (size <= 4)
        {
            _use64Bit = false;
            Logger.Information("Using 32-bit FNV-1a hash output.");
        }
        else
        {
            _use64Bit = true;
            Logger.Information("Using 64-bit FNV-1a hash output.");
        }

        return this;
    }

    /// <inheritdoc />
    public override Result<IHasher, HashingError> WithHashSizeValidated(int size)
    {
        // For FNV-1a, we support two sizes: 4 bytes (32-bit) and 8 bytes (64-bit)
        if (size <= 4)
        {
            _use64Bit = false;
            Logger.Information("Using 32-bit FNV-1a hash output.");
        }
        else
        {
            _use64Bit = true;
            Logger.Information("Using 64-bit FNV-1a hash output.");
        }

        return Result<IHasher, HashingError>.Success(this);
    }

    /// <inheritdoc />
    public override async Task<Result<string, HashingError>> HashAsync(
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
            // FNV-1a is very fast, so we only need to use Task.Run for very large inputs
            if (input.Length > 10000)
            {
                Logger.Information("Hashing input with FNV-1a ({0}-bit) asynchronously.", _use64Bit ? 64 : 32);

                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var byteCount = Encoding.UTF8.GetByteCount(input);
                    var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
                    try
                    {
                        var bufferSpan = buffer.AsSpan(0, byteCount);
                        Encoding.UTF8.GetBytes(input, bufferSpan);

                        if (_use64Bit)
                        {
                            var hashVal = Fnv1a.Hash64(bufferSpan);
                            return Result<string, HashingError>.Success(hashVal.ToString("x16"));
                        }
                        else
                        {
                            var hashVal = Fnv1a.Hash32(bufferSpan);
                            return Result<string, HashingError>.Success(hashVal.ToString("x8"));
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
    public override Result<string, HashingError> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("FNV-1a: Input cannot be null or empty (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input with FNV-1a ({0}-bit) sync.", _use64Bit ? 64 : 32);
            var byteCount = Encoding.UTF8.GetByteCount(input);

            // Use stackalloc for small inputs, ArrayPool for larger inputs
            if (byteCount <= 512)
            {
                Span<byte> buffer = stackalloc byte[byteCount];
                Encoding.UTF8.GetBytes(input, buffer);

                if (_use64Bit)
                {
                    var hashVal = Fnv1a.Hash64(buffer);
                    return Result<string, HashingError>.Success(hashVal.ToString("x16"));
                }
                else
                {
                    var hashVal = Fnv1a.Hash32(buffer);
                    return Result<string, HashingError>.Success(hashVal.ToString("x8"));
                }
            }
            else
            {
                var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
                try
                {
                    var bufferSpan = buffer.AsSpan(0, byteCount);
                    Encoding.UTF8.GetBytes(input, bufferSpan);

                    if (_use64Bit)
                    {
                        var hashVal = Fnv1a.Hash64(bufferSpan);
                        return Result<string, HashingError>.Success(hashVal.ToString("x16"));
                    }
                    else
                    {
                        var hashVal = Fnv1a.Hash32(bufferSpan);
                        return Result<string, HashingError>.Success(hashVal.ToString("x8"));
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
            Logger.Error(ex, "Error during FNV-1a hashing (sync).");
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
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
        {
            Logger.Error("FNV-1a: Input and expected hash cannot be null or empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        // Check if we're dealing with 32-bit or 64-bit based on hash length
        _use64Bit = expectedHash.Length > 8;

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
    public override Result<Unit, HashingError> Verify(string input, string expectedHash)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
        {
            Logger.Error("FNV-1a: Input and expected hash cannot be null or empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        // Check if we're dealing with 32-bit or 64-bit based on hash length
        _use64Bit = expectedHash.Length > 8;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override async Task<Result<string, HashingError>> EncodeToBase64HashAsync(
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
            Logger.Information("Encoding data to Base64 with FNV-1a ({0}-bit) async.", _use64Bit ? 64 : 32);

            // Only use Task.Run for larger data
            if (data.Length > 10000)
            {
                return await Task.Run(() =>
                {
                    if (_use64Bit)
                    {
                        var hashVal = Fnv1a.Hash64(data.Span);
                        Span<byte> hashBytes = stackalloc byte[8];
                        BitConverter.TryWriteBytes(hashBytes, hashVal);
                        return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
                    }
                    else
                    {
                        var hashVal = Fnv1a.Hash32(data.Span);
                        Span<byte> hashBytes = stackalloc byte[4];
                        BitConverter.TryWriteBytes(hashBytes, hashVal);
                        return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
                    }
                }, cancellationToken).ConfigureAwait(false);
            }

            {
                if (_use64Bit)
                {
                    var hashVal = Fnv1a.Hash64(data.Span);
                    Span<byte> hashBytes = stackalloc byte[8];
                    BitConverter.TryWriteBytes(hashBytes, hashVal);
                    return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
                }
                else
                {
                    var hashVal = Fnv1a.Hash32(data.Span);
                    Span<byte> hashBytes = stackalloc byte[4];
                    BitConverter.TryWriteBytes(hashBytes, hashVal);
                    return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
                }
            }
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Result<string, HashingError> EncodeToBase64Hash(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("FNV-1a: Data cannot be null or empty for base64 hash (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Encoding data to Base64 with FNV-1a ({0}-bit) sync.", _use64Bit ? 64 : 32);

            if (_use64Bit)
            {
                var hashVal = Fnv1a.Hash64(data);
                Span<byte> hashBytes = stackalloc byte[8];
                BitConverter.TryWriteBytes(hashBytes, hashVal);
                return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
            }
            else
            {
                var hashVal = Fnv1a.Hash32(data);
                Span<byte> hashBytes = stackalloc byte[4];
                BitConverter.TryWriteBytes(hashBytes, hashVal);
                return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during FNV-1a base64 encoding (sync).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }
}
