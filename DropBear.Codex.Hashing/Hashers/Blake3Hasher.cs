#region

using System.Runtime.CompilerServices;
using System.Text;
using Blake3;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Hashing.Errors;
using DropBear.Codex.Hashing.Interfaces;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     A <see cref="IHasher" /> implementation using the Blake3 algorithm.
///     Supports variable output size; does not support salt or iterations.
/// </summary>
public class Blake3Hasher : BaseHasher
{
    private int _hashSize = 32; // Default to 32 bytes (256-bit)

    /// <summary>
    ///     Initializes a new instance of <see cref="Blake3Hasher" />.
    /// </summary>
    public Blake3Hasher() : base("Blake3")
    {
    }

    /// <inheritdoc />
    public override IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("Blake3 does not support salt. No-op.");
        return this;
    }

    /// <inheritdoc />
    public override Result<IHasher, HashingError> WithSaltValidated(byte[]? salt)
    {
        Logger.Information("Blake3 does not support salt. No-op.");
        return Result<IHasher, HashingError>.Success(this);
    }

    /// <inheritdoc />
    public override IHasher WithIterations(int iterations)
    {
        Logger.Information("Blake3 does not support iterations. No-op.");
        return this;
    }

    /// <inheritdoc />
    public override Result<IHasher, HashingError> WithIterationsValidated(int iterations)
    {
        Logger.Information("Blake3 does not support iterations. No-op.");
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
            Logger.Error("Blake3 hash size must be at least 1 byte.");
            return Result<IHasher, HashingError>.Failure(
                new HashComputationError("Hash size must be at least 1 byte for Blake3 hashing."));
        }

        // Blake3 supports arbitrary output size
        Logger.Information("Setting Blake3 hash output size to {Size} bytes.", size);
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
            Logger.Error("Blake3: Input cannot be null or empty.");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input using Blake3 (async).");
            cancellationToken.ThrowIfCancellationRequested();

            // For larger inputs, offload to task thread
            if (input.Length > 1000)
            {
                return await Task.Run(() =>
                {
                    var outputBytes = HashInput(Encoding.UTF8.GetBytes(input));
                    return Result<string, HashingError>.Success(Convert.ToBase64String(outputBytes));
                }, cancellationToken).ConfigureAwait(false);
            }

            {
                var outputBytes = HashInput(Encoding.UTF8.GetBytes(input));
                return Result<string, HashingError>.Success(Convert.ToBase64String(outputBytes));
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Blake3 hashing was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 hashing (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public override Result<string, HashingError> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("Blake3: Input cannot be null or empty.");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input using Blake3 (sync).");
            var outputBytes = HashInput(Encoding.UTF8.GetBytes(input));
            return Result<string, HashingError>.Success(Convert.ToBase64String(outputBytes));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 hashing (sync).");
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
                Logger.Error("Blake3 verification: input/expectedHash cannot be null or empty.");
                return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Logger.Information("Verifying hash using Blake3 (async).");

            byte[] computedHash;

            // Only use Task.Run for larger inputs
            if (input.Length > 1000)
            {
                computedHash = await Task.Run(() => HashInput(Encoding.UTF8.GetBytes(input)), cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                computedHash = HashInput(Encoding.UTF8.GetBytes(input));
            }

            byte[] expectedBytes;
            try
            {
                expectedBytes = Convert.FromBase64String(expectedHash);
            }
            catch (FormatException ex)
            {
                Logger.Error(ex, "Invalid base64 format for expected Blake3 hash.");
                return Result<Unit, HashingError>.Failure(HashVerificationError.InvalidFormat, ex);
            }

            // Use constant-time comparison for security
            var isValid = ConstantTimeEquals(computedHash, expectedBytes);

            return isValid
                ? Result<Unit, HashingError>.Success(Unit.Value)
                : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Blake3 verification was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 verification (async).");
            return Result<Unit, HashingError>.Failure(
                new HashVerificationError($"Error during verification: {ex.Message}"), ex);
        }
    }

    /// <inheritdoc />
    public override Result<Unit, HashingError> Verify(string input, string expectedHash)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
        {
            Logger.Error("Blake3 verification: input and expected hash cannot be null or empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        try
        {
            Logger.Information("Verifying hash using Blake3 (sync).");
            var computedHash = HashInput(Encoding.UTF8.GetBytes(input));

            byte[] expectedBytes;
            try
            {
                expectedBytes = Convert.FromBase64String(expectedHash);
            }
            catch (FormatException ex)
            {
                Logger.Error(ex, "Invalid base64 format for expected Blake3 hash.");
                return Result<Unit, HashingError>.Failure(HashVerificationError.InvalidFormat, ex);
            }

            // Use constant-time comparison for security
            var isValid = ConstantTimeEquals(computedHash, expectedBytes);

            return isValid
                ? Result<Unit, HashingError>.Success(Unit.Value)
                : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 verification (sync).");
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
            Logger.Error("Blake3 base64 encode: data cannot be empty.");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using Blake3 (async).");
            cancellationToken.ThrowIfCancellationRequested();

            // Process larger data asynchronously
            if (data.Length > 1000)
            {
                var result = await Task.Run(() => HashBytes(data.Span), cancellationToken).ConfigureAwait(false);
                return Result<string, HashingError>.Success(Convert.ToBase64String(result));
            }
            else
            {
                var result = HashBytes(data.Span);
                return Result<string, HashingError>.Success(Convert.ToBase64String(result));
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Blake3 base64 encode was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 base64 encoding (async).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public override Result<string, HashingError> EncodeToBase64Hash(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("Blake3 base64 encode: data cannot be null or empty.");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using Blake3 (sync).");
            var result = HashBytes(data);
            return Result<string, HashingError>.Success(Convert.ToBase64String(result));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 base64 encoding (sync).");
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    // -------------------------------------------------------------------------------
    // Private helper methods
    // -------------------------------------------------------------------------------

    /// <summary>
    ///     Computes a Blake3 hash of the input bytes with specified size.
    /// </summary>
    /// <param name="input">The input bytes to hash.</param>
    /// <returns>The hash with the specified output size.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] HashInput(byte[] input)
    {
        // In Blake3, we can extract specific hash sizes
        var hash = Hasher.Hash(input);

        // If the hash size matches the default Blake3 output, return as is
        if (_hashSize == 32)
        {
            return hash.AsSpan().ToArray();
        }

        // Custom sized output
        var result = new byte[_hashSize];

        // Copy only what we need, handling edge cases
        hash.AsSpan().Slice(0, Math.Min(_hashSize, hash.AsSpan().Length)).CopyTo(result.AsSpan());

        return result;
    }

    /// <summary>
    ///     Computes a Blake3 hash with the specified output size.
    /// </summary>
    /// <param name="data">The input data to hash.</param>
    /// <returns>The hash output with specified size.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] HashBytes(ReadOnlySpan<byte> data)
    {
        var hash = Hasher.Hash(data);

        // Handle specific hash output size
        if (_hashSize == 32)
        {
            return hash.AsSpan().ToArray();
        }

        // Custom output size
        var result = new byte[_hashSize];
        hash.AsSpan().Slice(0, Math.Min(_hashSize, hash.AsSpan().Length)).CopyTo(result.AsSpan());

        return result;
    }
}
