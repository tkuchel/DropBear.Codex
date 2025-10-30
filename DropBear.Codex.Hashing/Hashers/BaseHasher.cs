#region

using System.Buffers;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Hashing.Errors;
using DropBear.Codex.Hashing.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     Abstract base class for hashers that provides common functionality and default implementations.
///     Helps reduce code duplication across different hasher implementations.
/// </summary>
public abstract class BaseHasher : IHasher
{
    /// <summary>
    ///     Algorithm name for the specific hasher - used in logging.
    /// </summary>
    protected readonly string AlgorithmName;

    /// <summary>
    ///     Logger for the specific hasher implementation.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    ///     Initializes a new instance of <see cref="BaseHasher" /> with a logger and algorithm name.
    /// </summary>
    /// <param name="algorithmName">The name of the hashing algorithm, used in log messages.</param>
    protected BaseHasher(string algorithmName)
    {
        AlgorithmName = algorithmName;
        Logger = LoggerFactory.Logger.ForContext(GetType());
    }

    /// <inheritdoc />
    public abstract IHasher WithSalt(byte[]? salt);

    /// <inheritdoc />
    public abstract Result<IHasher, HashingError> WithSaltValidated(byte[]? salt);

    /// <inheritdoc />
    public abstract IHasher WithIterations(int iterations);

    /// <inheritdoc />
    public abstract Result<IHasher, HashingError> WithIterationsValidated(int iterations);

    /// <inheritdoc />
    public abstract IHasher WithHashSize(int size);

    /// <inheritdoc />
    public abstract Result<IHasher, HashingError> WithHashSizeValidated(int size);

    /// <inheritdoc />
    public abstract Task<Result<string, HashingError>> HashAsync(string input,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Result<string, HashingError> Hash(string input);

    /// <inheritdoc />
    public abstract Task<Result<Unit, HashingError>> VerifyAsync(string input, string expectedHash,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Result<Unit, HashingError> Verify(string input, string expectedHash);

    /// <inheritdoc />
    public abstract Task<Result<string, HashingError>> EncodeToBase64HashAsync(ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Result<string, HashingError> EncodeToBase64Hash(byte[] data);

    /// <inheritdoc />
    public virtual async Task<Result<Unit, HashingError>> VerifyBase64HashAsync(
        ReadOnlyMemory<byte> data,
        string expectedBase64Hash,
        CancellationToken cancellationToken = default)
    {
        if (data.IsEmpty)
        {
            Logger.Error("{Algorithm}: Data cannot be null or empty for base64 hash verification.", AlgorithmName);
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        try
        {
            Logger.Information("Verifying Base64 hash using {Algorithm}.", AlgorithmName);
            cancellationToken.ThrowIfCancellationRequested();

            var encodeResult = await EncodeToBase64HashAsync(data, cancellationToken).ConfigureAwait(false);
            if (!encodeResult.IsSuccess)
            {
                Logger.Error("Failed to compute {Algorithm} Base64 hash for verification.", AlgorithmName);
                return Result<Unit, HashingError>.Failure(encodeResult.Error!);
            }

            var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
            Logger.Information(isValid
                ? "{Algorithm} Base64 hash verification succeeded."
                : "{Algorithm} Base64 hash verification failed.", AlgorithmName);

            return isValid
                ? Result<Unit, HashingError>.Success(Unit.Value)
                : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("{Algorithm} base64 verification was canceled.", AlgorithmName);
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error verifying base64 hash with {Algorithm}.", AlgorithmName);
            return Result<Unit, HashingError>.Failure(
                new HashVerificationError($"Error during base64 verification: {ex.Message}"), ex);
        }
    }

    /// <inheritdoc />
    public virtual Result<Unit, HashingError> VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("{Algorithm}: Data cannot be null or empty for base64 hash verification.", AlgorithmName);
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        try
        {
            Logger.Information("Verifying Base64 hash using {Algorithm}.", AlgorithmName);
            var encodeResult = EncodeToBase64Hash(data);
            if (!encodeResult.IsSuccess)
            {
                Logger.Error("Failed to compute {Algorithm} Base64 hash for verification.", AlgorithmName);
                return Result<Unit, HashingError>.Failure(encodeResult.Error!);
            }

            var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
            Logger.Information(isValid
                ? "{Algorithm} Base64 hash verification succeeded."
                : "{Algorithm} Base64 hash verification failed.", AlgorithmName);

            return isValid
                ? Result<Unit, HashingError>.Success(Unit.Value)
                : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error verifying base64 hash with {Algorithm}.", AlgorithmName);
            return Result<Unit, HashingError>.Failure(
                new HashVerificationError($"Error during base64 verification: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Gets a byte array that can be used for temporary operations, with specified minimum size.
    ///     Uses thread-local storage to optimize repeated allocations in the same thread.
    /// </summary>
    /// <param name="minSize">Minimum required size of the buffer.</param>
    /// <returns>A byte array of at least <paramref name="minSize" /> length.</returns>
    protected byte[] GetTemporaryBuffer(int minSize)
    {
        // For smaller arrays, use ArrayPool
        return ArrayPool<byte>.Shared.Rent(minSize);
    }

    /// <summary>
    ///     Returns a borrowed buffer to the ArrayPool.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    protected void ReturnTemporaryBuffer(byte[] buffer)
    {
        if (buffer != null)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    ///     Checks if two byte arrays have identical content, in a time-constant way.
    ///     This helps prevent timing attacks for security-sensitive hash comparisons.
    /// </summary>
    /// <param name="a">First byte array.</param>
    /// <param name="b">Second byte array.</param>
    /// <returns>True if the arrays contain identical data.</returns>
    protected static bool ConstantTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }
}
