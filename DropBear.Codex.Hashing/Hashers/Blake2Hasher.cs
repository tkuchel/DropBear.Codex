#region

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using Blake2Fast;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Hashing.Errors;
using DropBear.Codex.Hashing.Helpers;
using DropBear.Codex.Hashing.Interfaces;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     A <see cref="IHasher" /> implementation using the Blake2b algorithm.
///     Supports an optional salt (combined with input) for hashing, and returns results as strongly-typed
///     <see cref="Result{T,TError}" />.
/// </summary>
public sealed class Blake2Hasher : BaseHasher
{
    private int _hashSize = 32; // Default: 32 bytes
    private byte[]? _salt;

    /// <summary>
    ///     Initializes a new instance of <see cref="Blake2Hasher" />.
    /// </summary>
    public Blake2Hasher() : base("Blake2b")
    {
    }

    /// <inheritdoc />
    public override IHasher WithSalt(byte[]? salt)
    {
        var result = WithSaltValidated(salt);
        if (!result.IsSuccess)
        {
            throw new ArgumentException(result.Error!.Message, nameof(salt));
        }

        return result.Value!;
    }

    /// <inheritdoc />
    public override Result<IHasher, HashingError> WithSaltValidated(byte[]? salt)
    {
        if (salt is null || salt.Length == 0)
        {
            Logger.Error("Salt cannot be null or empty.");
            return Result<IHasher, HashingError>.Failure(
                new HashComputationError("Salt cannot be null or empty for Blake2 hashing."));
        }

        Logger.Information("Setting salt for Blake2 hashing.");
        _salt = salt;
        return Result<IHasher, HashingError>.Success(this);
    }

    /// <inheritdoc />
    public override IHasher WithIterations(int iterations)
    {
        // Not applicable for Blake2; do nothing but log
        Logger.Information("WithIterations called but not applicable for Blake2Hasher.");
        return this;
    }

    /// <inheritdoc />
    public override Result<IHasher, HashingError> WithIterationsValidated(int iterations)
    {
        // Not applicable for Blake2; do nothing but log
        Logger.Information("WithIterationsValidated called but not applicable for Blake2Hasher.");
        return Result<IHasher, HashingError>.Success(this);
    }

    /// <inheritdoc />
    public override IHasher WithHashSize(int size)
    {
        var result = WithHashSizeValidated(size);
        if (!result.IsSuccess)
        {
            throw new ArgumentOutOfRangeException(nameof(size), result.Error!.Message);
        }

        return result.Value!;
    }

    /// <inheritdoc />
    public override Result<IHasher, HashingError> WithHashSizeValidated(int size)
    {
        if (size < 1)
        {
            Logger.Error("Hash size must be at least 1 byte for Blake2.");
            return Result<IHasher, HashingError>.Failure(
                new HashComputationError("Hash size must be at least 1 byte for Blake2 hashing."));
        }

        Logger.Information("Setting hash size for Blake2 hashing to {Size} bytes.", size);
        _hashSize = size;
        return Result<IHasher, HashingError>.Success(this);
    }

    /// <inheritdoc />
    public override async Task<Result<string, HashingError>> HashAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("Input cannot be null or empty for Blake2 hashing.");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input using Blake2 (async).");
            cancellationToken.ThrowIfCancellationRequested();

            // Use Task.Run only for larger inputs to justify the overhead
            byte[] hashBytes;
            if (input.Length > 1000)
            {
                hashBytes = await Task.Run(() =>
                {
                    _salt ??= HashingHelper.GenerateRandomSalt(32);
                    return HashWithBlake2(input, _salt);
                }, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _salt ??= HashingHelper.GenerateRandomSalt(32);
                hashBytes = HashWithBlake2(input, _salt);
            }

            var combinedBytes = CombineBytes(_salt, hashBytes);
            return Result<string, HashingError>.Success(Convert.ToBase64String(combinedBytes));
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Blake2 hashing operation was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error computing Blake2 hash (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public override Result<string, HashingError> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("Input cannot be null or empty for Blake2 hashing.");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input using Blake2 (sync).");
            _salt ??= HashingHelper.GenerateRandomSalt(32);

            var hashBytes = HashWithBlake2(input, _salt);
            var combinedBytes = CombineBytes(_salt, hashBytes);

            return Result<string, HashingError>.Success(Convert.ToBase64String(combinedBytes));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error computing Blake2 hash (sync).");
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
        try
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
            {
                Logger.Error("Input and expected hash cannot be null or empty for Blake2 verification.");
                return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
            }

            cancellationToken.ThrowIfCancellationRequested();

            Logger.Information("Verifying Blake2 hash (async).");
            byte[] expectedBytes;

            try
            {
                expectedBytes = Convert.FromBase64String(expectedHash);
            }
            catch (FormatException ex)
            {
                Logger.Error(ex, "Invalid base64 format for Blake2 expected hash.");
                return Result<Unit, HashingError>.Failure(HashVerificationError.InvalidFormat, ex);
            }

            var saltLength = _salt?.Length ?? 32;
            var (salt, expectedHashBytes) = HashingHelper.ExtractBytes(expectedBytes, saltLength);

            byte[] hashBytes;
            if (input.Length > 1000)
            {
                hashBytes = await Task.Run(() => HashWithBlake2(input, salt), cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                hashBytes = HashWithBlake2(input, salt);
            }

            // Use constant-time comparison to prevent timing attacks
            var isValid = ConstantTimeEquals(hashBytes, expectedHashBytes);

            return isValid
                ? Result<Unit, HashingError>.Success(Unit.Value)
                : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Blake2 verification operation was canceled.");
            throw;
        }
        catch (FormatException ex)
        {
            Logger.Error(ex, "Invalid format for Blake2 expected hash (async).");
            return Result<Unit, HashingError>.Failure(HashVerificationError.InvalidFormat, ex);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake2 verification (async).");
            return Result<Unit, HashingError>.Failure(
                new HashVerificationError($"Error during verification: {ex.Message}"), ex);
        }
    }

    /// <inheritdoc />
    public override Result<Unit, HashingError> Verify(string input, string expectedHash)
    {
        try
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
            {
                Logger.Error("Blake2 verification: input/expectedHash cannot be null or empty.");
                return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
            }

            Logger.Information("Verifying Blake2 hash (sync).");
            byte[] expectedBytes;

            try
            {
                expectedBytes = Convert.FromBase64String(expectedHash);
            }
            catch (FormatException ex)
            {
                Logger.Error(ex, "Invalid base64 format for Blake2 expected hash (sync).");
                return Result<Unit, HashingError>.Failure(HashVerificationError.InvalidFormat, ex);
            }

            var saltLength = _salt?.Length ?? 32;
            var (salt, expectedHashBytes) = HashingHelper.ExtractBytes(expectedBytes, saltLength);

            var hashBytes = HashWithBlake2(input, salt);

            // Use constant-time comparison to prevent timing attacks
            var isValid = ConstantTimeEquals(hashBytes, expectedHashBytes);

            return isValid
                ? Result<Unit, HashingError>.Success(Unit.Value)
                : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
        }
        catch (FormatException ex)
        {
            Logger.Error(ex, "Invalid format for Blake2 expected hash (sync).");
            return Result<Unit, HashingError>.Failure(HashVerificationError.InvalidFormat, ex);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake2 verification (sync).");
            return Result<Unit, HashingError>.Failure(
                new HashVerificationError($"Error during verification: {ex.Message}"), ex);
        }
    }

    /// <inheritdoc />
    public override async Task<Result<string, HashingError>> EncodeToBase64HashAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        if (data.IsEmpty)
        {
            Logger.Error("Blake2 base64 encode: data cannot be empty.");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using Blake2 (async).");
            cancellationToken.ThrowIfCancellationRequested();

            // Only use Task.Run for larger data
            byte[] hash;
            if (data.Length > 1000)
            {
                hash = await Task.Run(() => Blake2b.ComputeHash(_hashSize, data.Span), cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                hash = Blake2b.ComputeHash(_hashSize, data.Span);
            }

            return Result<string, HashingError>.Success(Convert.ToBase64String(hash));
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Blake2 EncodeToBase64Hash operation canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake2 base64 encoding (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public override Result<string, HashingError> EncodeToBase64Hash(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("Data cannot be null or empty for Blake2 base64 encoding (sync).");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using Blake2 (sync).");
            var hash = Blake2b.ComputeHash(_hashSize, data);
            return Result<string, HashingError>.Success(Convert.ToBase64String(hash));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake2 base64 encoding (sync).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    // -------------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------------

    /// <summary>
    ///     Hashes input with Blake2b, optionally combining with a salt.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <param name="salt">Optional salt bytes to combine with input.</param>
    /// <returns>The computed hash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] HashWithBlake2(string input, byte[]? salt)
    {
        Logger.Debug("Hashing input using Blake2 with optional salt (private helper).");

        // Calculate the size needed for UTF8 bytes
        var inputByteCount = Encoding.UTF8.GetByteCount(input);
        byte[]? rentedInputBuffer = null;

        try
        {
            // Use stackalloc for small inputs, ArrayPool for large inputs
            Span<byte> inputBytes = inputByteCount <= 512
                ? stackalloc byte[inputByteCount]
                : (rentedInputBuffer = ArrayPool<byte>.Shared.Rent(inputByteCount)).AsSpan(0, inputByteCount);

            Encoding.UTF8.GetBytes(input, inputBytes);

            if (salt == null || salt.Length == 0)
            {
                return Blake2b.ComputeHash(_hashSize, inputBytes);
            }

            // Use more efficient memory handling with stackalloc for small data
            ReadOnlySpan<byte> saltSpan = salt;
            var bufferSize = saltSpan.Length + inputByteCount;

            if (bufferSize <= 1024)
            {
                // For small inputs, use stack allocation for better performance
                Span<byte> saltedInput = stackalloc byte[bufferSize];
                saltSpan.CopyTo(saltedInput);
                inputBytes.CopyTo(saltedInput.Slice(saltSpan.Length));

                return Blake2b.ComputeHash(_hashSize, saltedInput);
            }

            // For larger inputs, use ArrayPool to minimize GC pressure
            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                var bufferSpan = buffer.AsSpan(0, bufferSize);
                saltSpan.CopyTo(bufferSpan);
                inputBytes.CopyTo(bufferSpan.Slice(saltSpan.Length));

                return Blake2b.ComputeHash(_hashSize, bufferSpan.Slice(0, bufferSize));
            }
            finally
            {
                // Clear sensitive data before returning
                Array.Clear(buffer, 0, bufferSize);
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        finally
        {
            // Return rented buffer if we used one
            if (rentedInputBuffer != null)
            {
                Array.Clear(rentedInputBuffer, 0, inputByteCount);
                ArrayPool<byte>.Shared.Return(rentedInputBuffer);
            }
        }
    }

    /// <summary>
    ///     Combines salt and hash bytes efficiently.
    /// </summary>
    /// <param name="salt">The salt bytes.</param>
    /// <param name="hash">The hash bytes.</param>
    /// <returns>Combined array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] CombineBytes(byte[] salt, byte[] hash)
    {
        // Create a single array for the combined data
        var result = new byte[salt.Length + hash.Length];

        // Copy salt and hash into the result array
        salt.AsSpan().CopyTo(result.AsSpan(0, salt.Length));
        hash.AsSpan().CopyTo(result.AsSpan(salt.Length));

        return result;
    }
}
