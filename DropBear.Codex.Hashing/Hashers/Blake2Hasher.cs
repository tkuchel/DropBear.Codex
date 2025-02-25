#region

using System.Collections;
using System.Text;
using Blake2Fast;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Hashing.Errors;
using DropBear.Codex.Hashing.Helpers;
using DropBear.Codex.Hashing.Interfaces;
using Serilog;
// For Result<T, HashingError> etc.
// For HashComputationError, HashVerificationError, etc.
// For HashingHelper

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     A <see cref="IHasher" /> implementation using the Blake2b algorithm.
///     Supports an optional salt (combined with input) for hashing, and returns results as strongly-typed
///     <see cref="Result{T,TError}" />.
/// </summary>
public sealed class Blake2Hasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Blake2Hasher>();

    private int _hashSize = 32; // Default: 32 bytes
    private byte[]? _salt;

    /// <inheritdoc />
    public IHasher WithSalt(byte[]? salt)
    {
        if (salt is null || salt.Length == 0)
        {
            Logger.Error("Salt cannot be null or empty.");
            throw new ArgumentException("Salt cannot be null or empty.", nameof(salt));
        }

        Logger.Information("Setting salt for Blake2 hashing.");
        _salt = salt;
        return this;
    }

    /// <inheritdoc />
    public IHasher WithIterations(int iterations)
    {
        // Not applicable for Blake2; do nothing but log
        Logger.Information("WithIterations called but not applicable for Blake2Hasher.");
        return this;
    }

    /// <inheritdoc />
    public IHasher WithHashSize(int size)
    {
        if (size < 1)
        {
            Logger.Error("Hash size must be at least 1 byte for Blake2.");
            throw new ArgumentOutOfRangeException(nameof(size), "Hash size must be at least 1 byte.");
        }

        Logger.Information("Setting hash size for Blake2 hashing to {Size} bytes.", size);
        _hashSize = size;
        return this;
    }

    /// <inheritdoc />
    public async Task<Result<string, HashingError>> HashAsync(
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

            var hashBytes = await Task.Run(() =>
            {
                _salt ??= HashingHelper.GenerateRandomSalt(32);
                return HashWithBlake2(input, _salt);
            }, cancellationToken).ConfigureAwait(false);

            var combinedBytes = HashingHelper.CombineBytes(_salt, hashBytes);
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
    public Result<string, HashingError> Hash(string input)
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
            var combinedBytes = HashingHelper.CombineBytes(_salt, hashBytes);

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
    public async Task<Result<Unit, HashingError>> VerifyAsync(
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
            var expectedBytes = Convert.FromBase64String(expectedHash);

            var saltLength = _salt?.Length ?? 32;
            var (salt, expectedHashBytes) = HashingHelper.ExtractBytes(expectedBytes, saltLength);

            var hashBytes = await Task.Run(() => HashWithBlake2(input, salt), cancellationToken).ConfigureAwait(false);
            var isValid = StructuralComparisons.StructuralEqualityComparer.Equals(hashBytes, expectedHashBytes);

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
    public Result<Unit, HashingError> Verify(string input, string expectedHash)
    {
        try
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
            {
                Logger.Error("Blake2 verification: input/expectedHash cannot be null or empty.");
                return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
            }

            Logger.Information("Verifying Blake2 hash (sync).");
            var expectedBytes = Convert.FromBase64String(expectedHash);

            var saltLength = _salt?.Length ?? 32;
            var (salt, expectedHashBytes) = HashingHelper.ExtractBytes(expectedBytes, saltLength);

            var hashBytes = HashWithBlake2(input, salt);
            var isValid = StructuralComparisons.StructuralEqualityComparer.Equals(hashBytes, expectedHashBytes);

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
    public async Task<Result<string, HashingError>> EncodeToBase64HashAsync(
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

            var hash = await Task.Run(() => Blake2b.ComputeHash(_hashSize, data.ToArray()), cancellationToken)
                .ConfigureAwait(false);

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
    public Result<string, HashingError> EncodeToBase64Hash(byte[] data)
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

    /// <inheritdoc />
    public async Task<Result<Unit, HashingError>> VerifyBase64HashAsync(
        ReadOnlyMemory<byte> data,
        string expectedBase64Hash,
        CancellationToken cancellationToken = default)
    {
        if (data.IsEmpty)
        {
            Logger.Error("Blake2 base64 verification: data cannot be empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        try
        {
            Logger.Information("Verifying Base64 hash using Blake2 (async).");
            cancellationToken.ThrowIfCancellationRequested();

            var hashBase64 = await EncodeToBase64HashAsync(data, cancellationToken).ConfigureAwait(false);
            if (!hashBase64.IsSuccess)
            {
                return Result<Unit, HashingError>.Failure(hashBase64.Error);
            }

            var isValid = string.Equals(hashBase64.Value, expectedBase64Hash, StringComparison.Ordinal);
            return isValid
                ? Result<Unit, HashingError>.Success(Unit.Value)
                : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Blake2 VerifyBase64Hash operation was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error verifying base64 hash with Blake2 (async).");
            return Result<Unit, HashingError>.Failure(
                new HashVerificationError($"Error during base64 verification: {ex.Message}"), ex);
        }
    }

    /// <inheritdoc />
    public Result<Unit, HashingError> VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("Blake2 base64 verification: data cannot be null or empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        try
        {
            Logger.Information("Verifying Base64 hash using Blake2 (sync).");
            var hashResult = EncodeToBase64Hash(data);
            if (!hashResult.IsSuccess)
            {
                return Result<Unit, HashingError>.Failure(hashResult.Error);
            }

            var isValid = string.Equals(hashResult.Value, expectedBase64Hash, StringComparison.Ordinal);
            return isValid
                ? Result<Unit, HashingError>.Success(Unit.Value)
                : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error verifying base64 hash with Blake2 (sync).");
            return Result<Unit, HashingError>.Failure(
                new HashVerificationError($"Error during base64 verification: {ex.Message}"), ex);
        }
    }

    // -------------------------------------------------------------------------------
    // Private helper
    // -------------------------------------------------------------------------------
    private byte[] HashWithBlake2(string input, byte[]? salt)
    {
        Logger.Debug("Hashing input using Blake2 with optional salt (private helper).");
        var inputBytes = Encoding.UTF8.GetBytes(input);

        if (salt != null)
        {
            var saltedInput = HashingHelper.CombineBytes(salt, inputBytes);
            return Blake2b.ComputeHash(_hashSize, saltedInput);
        }

        return Blake2b.ComputeHash(_hashSize, inputBytes);
    }
}
