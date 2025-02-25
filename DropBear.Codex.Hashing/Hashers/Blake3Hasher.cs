#region

using System.Text;
using Blake3;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Hashing.Errors;
using DropBear.Codex.Hashing.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     A <see cref="IHasher" /> implementation using the Blake3 algorithm (fixed or variable output),
///     but here we treat it as a single fixed size. Does not support salt or iterations.
/// </summary>
public class Blake3Hasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Blake3Hasher>();

    /// <inheritdoc />
    public IHasher WithSalt(byte[]? salt)
    {
        Logger.Information("Blake3 does not support salt. No-op.");
        return this;
    }

    /// <inheritdoc />
    public IHasher WithIterations(int iterations)
    {
        Logger.Information("Blake3 does not support iterations. No-op.");
        return this;
    }

    /// <inheritdoc />
    public IHasher WithHashSize(int size)
    {
        // Blake3 can produce variable output, but we won't implement it here
        Logger.Information("Blake3 default output used. WithHashSize is a no-op for Blake3Hasher.");
        return this;
    }

    /// <inheritdoc />
    public async Task<Result<string, HashingError>> HashAsync(
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

            // We'll just do a quick Task.Run for demonstration
            var hashString = await Task.Run(() =>
            {
                var hash = Hasher.Hash(Encoding.UTF8.GetBytes(input));
                return hash.ToString();
            }, cancellationToken).ConfigureAwait(false);

            Logger.Information("Blake3 hashing successful (async).");
            return Result<string, HashingError>.Success(hashString);
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
    public Result<string, HashingError> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("Blake3: Input cannot be null or empty.");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Hashing input using Blake3 (sync).");
            var hash = Hasher.Hash(Encoding.UTF8.GetBytes(input));
            return Result<string, HashingError>.Success(hash.ToString());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 hashing (sync).");
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
                Logger.Error("Blake3 verification: input/expectedHash cannot be null or empty.");
                return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Logger.Information("Verifying hash using Blake3 (async).");

            var hashString = await Task.Run(() =>
            {
                var hash = Hasher.Hash(Encoding.UTF8.GetBytes(input));
                return hash.ToString();
            }, cancellationToken).ConfigureAwait(false);

            var isValid = string.Equals(hashString, expectedHash, StringComparison.Ordinal);
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
    public Result<Unit, HashingError> Verify(string input, string expectedHash)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedHash))
        {
            Logger.Error("Blake3 verification: input and expected hash cannot be null or empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        try
        {
            Logger.Information("Verifying hash using Blake3 (sync).");
            var hashString = Hasher.Hash(Encoding.UTF8.GetBytes(input)).ToString();
            var isValid = string.Equals(hashString, expectedHash, StringComparison.Ordinal);

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
    public async Task<Result<string, HashingError>> EncodeToBase64HashAsync(
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

            var hashBytes = await Task.Run(() =>
            {
                // Hasher.Hash(...) returns a Hash object with a .AsSpan() method
                // but we copy that to an array so we can safely pass it around
                var hash = Hasher.Hash(data.Span);
                return hash.AsSpan().ToArray();
            }, cancellationToken).ConfigureAwait(false);

            // Now Convert.ToBase64String(hashBytes) works fine
            return Result<string, HashingError>.Success(Convert.ToBase64String(hashBytes));
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
    public Result<string, HashingError> EncodeToBase64Hash(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("Blake3 base64 encode: data cannot be null or empty.");
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using Blake3 (sync).");
            var hash = Hasher.Hash(data);
            return Result<string, HashingError>.Success(Convert.ToBase64String(hash.AsSpan()));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Blake3 base64 encoding (sync).");
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
            Logger.Error("Blake3 base64 verification: data cannot be empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        try
        {
            Logger.Information("Verifying Base64 hash using Blake3 (async).");
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
            Logger.Information("Blake3 base64 verification was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error verifying base64 hash with Blake3 (async).");
            return Result<Unit, HashingError>.Failure(
                new HashVerificationError($"Error during base64 verification: {ex.Message}"), ex);
        }
    }

    /// <inheritdoc />
    public Result<Unit, HashingError> VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Error("Blake3 base64 verification: data cannot be null or empty.");
            return Result<Unit, HashingError>.Failure(HashVerificationError.MissingInput);
        }

        try
        {
            Logger.Information("Verifying Base64 hash using Blake3 (sync).");
            var hashBase64 = EncodeToBase64Hash(data);
            if (!hashBase64.IsSuccess)
            {
                return Result<Unit, HashingError>.Failure(hashBase64.Error);
            }

            var isValid = string.Equals(hashBase64.Value, expectedBase64Hash, StringComparison.Ordinal);
            return isValid
                ? Result<Unit, HashingError>.Success(Unit.Value)
                : Result<Unit, HashingError>.Failure(HashVerificationError.HashMismatch);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error verifying base64 hash with Blake3 (sync).");
            return Result<Unit, HashingError>.Failure(
                new HashVerificationError($"Error during base64 verification: {ex.Message}"), ex);
        }
    }
}
