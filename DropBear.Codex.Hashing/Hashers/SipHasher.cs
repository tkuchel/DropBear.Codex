#region

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Hashing.Errors;
using DropBear.Codex.Hashing.Interfaces;
using HashDepot;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     A <see cref="IHasher" /> implementation using SipHash-2-4 (64-bit).
///     Requires a 16-byte key, no salt or iteration support.
/// </summary>
public sealed class SipHasher : BaseHasher
{
    private byte[] _key;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SipHasher" /> class with a 16-byte key.
    /// </summary>
    /// <param name="key">A 16-byte key for SipHash.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key" /> is not 16 bytes.</exception>
    public SipHasher(byte[] key) : base("SipHash-2-4")
    {
        if (key == null || key.Length != 16)
        {
            Logger.Error("SipHash key must be 16 bytes in length.");
            throw new ArgumentException("Key must be 16 bytes in length.", nameof(key));
        }

        _key = key;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SipHasher" /> class with a random 16-byte key.
    /// </summary>
    public SipHasher() : base("SipHash-2-4")
    {
        _key = new byte[16];
        RandomNumberGenerator.Fill(_key);
        Logger.Information("SipHasher initialized with a random 16-byte key.");
    }

    /// <inheritdoc />
    public override IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("SipHash does not utilize salt. No-op.");
        return this;
    }

    /// <inheritdoc />
    public override Result<IHasher, HashingError> WithSaltValidated(byte[]? salt)
    {
        Logger.Information("SipHash does not utilize salt. No-op.");
        return Result<IHasher, HashingError>.Success(this);
    }

    /// <inheritdoc />
    public override IHasher WithIterations(int iterations)
    {
        Logger.Information("SipHash does not utilize iterations. No-op.");
        return this;
    }

    /// <inheritdoc />
    public override Result<IHasher, HashingError> WithIterationsValidated(int iterations)
    {
        Logger.Information("SipHash does not utilize iterations. No-op.");
        return Result<IHasher, HashingError>.Success(this);
    }

    /// <inheritdoc />
    public override IHasher WithHashSize(int size)
    {
        Logger.Information("SipHash-2-4 output is fixed at 64 bits. No-op.");
        return this;
    }

    /// <inheritdoc />
    public override Result<IHasher, HashingError> WithHashSizeValidated(int size)
    {
        Logger.Information("SipHash-2-4 output is fixed at 64 bits. No-op.");
        return Result<IHasher, HashingError>.Success(this);
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

    /// <inheritdoc />
    public override async Task<Result<string, HashingError>> HashAsync(
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
            // SipHash is fast, so only use Task.Run for larger inputs
            if (input.Length > 10000)
            {
                Logger.Information("Hashing input with SipHash-2-4 (async).");

                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var byteCount = Encoding.UTF8.GetByteCount(input);
                    var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
                    try
                    {
                        var bufferSpan = buffer.AsSpan(0, byteCount);
                        Encoding.UTF8.GetBytes(input, bufferSpan);
                        var hashVal = SipHash24.Hash64(bufferSpan, _key);
                        return Result<string, HashingError>.Success(hashVal.ToString("x16"));
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }, cancellationToken).ConfigureAwait(false);
            }

            return Hash(input);
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

    /// <inheritdoc />
    public override Result<string, HashingError> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("SipHash-2-4: input cannot be null or empty (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input with SipHash-2-4 (sync).");
            var byteCount = Encoding.UTF8.GetByteCount(input);

            // Use stackalloc for small inputs, ArrayPool for larger inputs
            if (byteCount <= 512)
            {
                Span<byte> buffer = stackalloc byte[byteCount];
                Encoding.UTF8.GetBytes(input, buffer);
                var hashVal = SipHash24.Hash64(buffer, _key);
                return Result<string, HashingError>.Success(hashVal.ToString("x16"));
            }
            else
            {
                var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
                try
                {
                    var bufferSpan = buffer.AsSpan(0, byteCount);
                    Encoding.UTF8.GetBytes(input, bufferSpan);
                    var hashVal = SipHash24.Hash64(bufferSpan, _key);
                    return Result<string, HashingError>.Success(hashVal.ToString("x16"));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during SipHash hashing (sync).");
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
            Logger.Error("SipHash: Input and expected hash cannot be null or empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

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

    /// <inheritdoc />
    public override Result<Unit, HashingError> Verify(string input, string expectedHash)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
        {
            Logger.Error("SipHash: Input and expected hash cannot be null or empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

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

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override async Task<Result<string, HashingError>> EncodeToBase64HashAsync(
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

            // SipHash is fast, so only use Task.Run for large data
            if (data.Length > 10000)
            {
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var hashVal = SipHash24.Hash64(data.Span, _key);
                    Span<byte> hashBytes = stackalloc byte[8];
                    BitConverter.TryWriteBytes(hashBytes, hashVal);
                    return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
                }, cancellationToken).ConfigureAwait(false);
            }

            {
                var hashVal = SipHash24.Hash64(data.Span, _key);
                Span<byte> hashBytes = stackalloc byte[8];
                BitConverter.TryWriteBytes(hashBytes, hashVal);
                return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
            }
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

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Result<string, HashingError> EncodeToBase64Hash(byte[] data)
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
            Span<byte> hashBytes = stackalloc byte[8];
            BitConverter.TryWriteBytes(hashBytes, hashVal);
            return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during SipHash base64 encoding (sync).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }
}
