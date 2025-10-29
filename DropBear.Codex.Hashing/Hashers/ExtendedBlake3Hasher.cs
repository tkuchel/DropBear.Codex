#region

using System.Buffers;
using Blake3;
using DropBear.Codex.Core.Results;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Hashing.Errors;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

/// <summary>
///     Provides extended methods for Blake3 hashing (incremental, MAC generation, key derivation, stream hashing).
///     Inherits from <see cref="Blake3Hasher" /> but adds static helper methods.
/// </summary>
public sealed class ExtendedBlake3Hasher : Blake3Hasher
{
    /// <summary>
    ///     Initializes a new instance of <see cref="ExtendedBlake3Hasher" />.
    /// </summary>
    public ExtendedBlake3Hasher()
    {
    }

    #region Result-Based Safe Methods

    /// <summary>
    ///     Performs an incremental Blake3 hash over multiple data segments using the Result pattern.
    /// </summary>
    /// <param name="dataSegments">An enumerable of byte arrays to be hashed in sequence.</param>
    /// <returns>A Result containing the final Blake3 hash as a hex string, or an error.</returns>
    public static Result<string, HashingError> IncrementalHashSafe(IEnumerable<byte[]> dataSegments)
    {
        if (dataSegments is null)
        {
            return Result<string, HashingError>.Failure(
                HashComputationError.EmptyInput);
        }

        try
        {
            using var hasher = Hasher.New();
            foreach (var segment in dataSegments)
            {
                if (segment is null)
                {
                    return Result<string, HashingError>.Failure(
                        HashComputationError.EmptyInput);
                }

                hasher.Update(segment);
            }

            return Result<string, HashingError>.Success(hasher.Finalize().ToString());
        }
        catch (Exception ex)
        {
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <summary>
    ///     Performs an incremental Blake3 hash over multiple data segments asynchronously using the Result pattern.
    /// </summary>
    /// <param name="dataSegments">An enumerable of byte arrays to be hashed in sequence.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A Result containing the final Blake3 hash as a hex string, or an error.</returns>
    public static async ValueTask<Result<string, HashingError>> IncrementalHashSafeAsync(
        IEnumerable<byte[]> dataSegments,
        CancellationToken cancellationToken = default)
    {
        if (dataSegments is null)
        {
            return Result<string, HashingError>.Failure(
                HashComputationError.EmptyInput);
        }

        try
        {
            var segmentsList = dataSegments.ToList();

            return await Task.Run(() =>
            {
                using var hasher = Hasher.New();
                foreach (var segment in segmentsList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (segment is null)
                    {
                        return Result<string, HashingError>.Failure(
                            HashComputationError.EmptyInput);
                    }

                    hasher.Update(segment);
                }

                return Result<string, HashingError>.Success(hasher.Finalize().ToString());
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result<string, HashingError>.Failure(
                new HashComputationError("Operation was canceled"));
        }
        catch (Exception ex)
        {
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <summary>
    ///     Generates a Blake3-based MAC (Message Authentication Code) using a 32-byte key with the Result pattern.
    /// </summary>
    /// <param name="data">The data to MAC.</param>
    /// <param name="key">A 32-byte key used for MAC generation.</param>
    /// <returns>A Result containing a hex string representing the MAC, or an error.</returns>
    public static Result<string, HashingError> GenerateMacSafe(byte[] data, byte[] key)
    {
        if (key is null || key.Length != 32)
        {
            return Result<string, HashingError>.Failure(
                new HashComputationError("Key must be 256 bits (32 bytes)."));
        }

        if (data is null)
        {
            return Result<string, HashingError>.Failure(
                HashComputationError.EmptyInput);
        }

        try
        {
            using var hasher = Hasher.NewKeyed(key);
            hasher.Update(data);

            return Result<string, HashingError>.Success(hasher.Finalize().ToString());
        }
        catch (Exception ex)
        {
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <summary>
    ///     Generates a Blake3-based MAC asynchronously for larger data using the Result pattern.
    /// </summary>
    /// <param name="data">The data to MAC.</param>
    /// <param name="key">A 32-byte key used for MAC generation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A Result containing a hex string representing the MAC, or an error.</returns>
    public static async ValueTask<Result<string, HashingError>> GenerateMacSafeAsync(
        byte[] data,
        byte[] key,
        CancellationToken cancellationToken = default)
    {
        if (key is null || key.Length != 32)
        {
            return Result<string, HashingError>.Failure(
                new HashComputationError("Key must be 256 bits (32 bytes)."));
        }

        if (data is null)
        {
            return Result<string, HashingError>.Failure(
                HashComputationError.EmptyInput);
        }

        try
        {
            // For small data, don't bother with async
            if (data.Length < 1000)
            {
                return GenerateMacSafe(data, key);
            }

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var hasher = Hasher.NewKeyed(key);
                hasher.Update(data);

                return Result<string, HashingError>.Success(hasher.Finalize().ToString());
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result<string, HashingError>.Failure(
                new HashComputationError("Operation was canceled"));
        }
        catch (Exception ex)
        {
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <summary>
    ///     Derives a new key from input keying material using Blake3 with the Result pattern.
    /// </summary>
    /// <param name="context">A byte array representing the context string for domain separation.</param>
    /// <param name="inputKeyingMaterial">The input keying material from which to derive a key.</param>
    /// <returns>A Result containing the newly derived key as a byte array, or an error.</returns>
    public static Result<byte[], HashingError> DeriveKeySafe(byte[] context, byte[] inputKeyingMaterial)
    {
        if (context is null)
        {
            return Result<byte[], HashingError>.Failure(
                new HashComputationError("Context cannot be null."));
        }

        if (inputKeyingMaterial is null)
        {
            return Result<byte[], HashingError>.Failure(
                HashComputationError.EmptyInput);
        }

        try
        {
            using var hasher = Hasher.NewDeriveKey(context);
            hasher.Update(inputKeyingMaterial);

            return Result<byte[], HashingError>.Success(hasher.Finalize().AsSpan().ToArray());
        }
        catch (Exception ex)
        {
            return Result<byte[], HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <summary>
    ///     Derives a new key from input keying material using Blake3 asynchronously with the Result pattern.
    /// </summary>
    /// <param name="context">A byte array representing the context string for domain separation.</param>
    /// <param name="inputKeyingMaterial">The input keying material from which to derive a key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A Result containing the newly derived key as a byte array, or an error.</returns>
    public static async ValueTask<Result<byte[], HashingError>> DeriveKeySafeAsync(
        byte[] context,
        byte[] inputKeyingMaterial,
        CancellationToken cancellationToken = default)
    {
        if (context is null)
        {
            return Result<byte[], HashingError>.Failure(
                new HashComputationError("Context cannot be null."));
        }

        if (inputKeyingMaterial is null)
        {
            return Result<byte[], HashingError>.Failure(
                HashComputationError.EmptyInput);
        }

        try
        {
            // For small inputs, just use the synchronous method
            if (inputKeyingMaterial.Length < 1000)
            {
                return DeriveKeySafe(context, inputKeyingMaterial);
            }

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var hasher = Hasher.NewDeriveKey(context);
                hasher.Update(inputKeyingMaterial);

                return Result<byte[], HashingError>.Success(hasher.Finalize().AsSpan().ToArray());
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result<byte[], HashingError>.Failure(
                new HashComputationError("Operation was canceled"));
        }
        catch (Exception ex)
        {
            return Result<byte[], HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <summary>
    ///     Hashes the contents of the provided <see cref="Stream" /> using Blake3 with the Result pattern.
    /// </summary>
    /// <param name="inputStream">The input stream to read and hash.</param>
    /// <returns>A Result containing the final hash as a hex string, or an error.</returns>
    public static Result<string, HashingError> HashStreamSafe(Stream inputStream)
    {
        if (inputStream is null)
        {
            return Result<string, HashingError>.Failure(
                new HashComputationError("Input stream cannot be null."));
        }

        try
        {
            using var hasher = Hasher.New();

            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            try
            {
                int bytesRead;
                while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hasher.Update(buffer.AsSpan(0, bytesRead));
                }

                return Result<string, HashingError>.Success(hasher.Finalize().ToString());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (Exception ex)
        {
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    /// <summary>
    ///     Asynchronously hashes the content of a stream using Blake3 with the Result pattern.
    /// </summary>
    /// <param name="inputStream">The stream to hash.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A Result containing the hash as a hex string, or an error.</returns>
    public static async ValueTask<Result<string, HashingError>> HashStreamSafeAsync(
        Stream inputStream,
        CancellationToken cancellationToken = default)
    {
        if (inputStream is null)
        {
            return Result<string, HashingError>.Failure(
                new HashComputationError("Input stream cannot be null."));
        }

        if (!inputStream.CanRead)
        {
            return Result<string, HashingError>.Failure(
                new HashComputationError("Stream must be readable."));
        }

        try
        {
            using var hasher = Hasher.New();

            var buffer = ArrayPool<byte>.Shared.Rent(16384);
            try
            {
                int bytesRead;
                while ((bytesRead = await inputStream.ReadAsync(
                           buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    hasher.Update(buffer.AsSpan(0, bytesRead));
                }

                return Result<string, HashingError>.Success(hasher.Finalize().ToString());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (OperationCanceledException)
        {
            return Result<string, HashingError>.Failure(
                new HashComputationError("Operation was canceled"));
        }
        catch (Exception ex)
        {
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    #endregion

    #region Legacy Exception-Based Methods (Obsolete)

    /// <summary>
    ///     Performs an incremental Blake3 hash over multiple data segments.
    /// </summary>
    /// <param name="dataSegments">An enumerable of byte arrays to be hashed in sequence.</param>
    /// <returns>The final Blake3 hash as a hex string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="dataSegments" /> is null or any segment is null.</exception>
    [Obsolete("Use IncrementalHashSafe instead for better error handling with the Result pattern.")]
    public static string IncrementalHash(IEnumerable<byte[]> dataSegments)
    {
        if (dataSegments is null)
        {
            throw new ArgumentNullException(nameof(dataSegments), "Data segments cannot be null.");
        }

        try
        {
            using var hasher = Hasher.New();
            foreach (var segment in dataSegments)
            {
                if (segment is null)
                {
                    throw new ArgumentNullException(nameof(dataSegments), "Data segment cannot be null.");
                }

                hasher.Update(segment);
            }

            return hasher.Finalize().ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during incremental Blake3 hash.", ex);
        }
    }

    /// <summary>
    ///     Performs an incremental Blake3 hash over multiple data segments asynchronously.
    /// </summary>
    /// <param name="dataSegments">An enumerable of byte arrays to be hashed in sequence.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The final Blake3 hash as a hex string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="dataSegments" /> is null or any segment is null.</exception>
    [Obsolete("Use IncrementalHashSafeAsync instead for better error handling with the Result pattern.")]
    public static async Task<string> IncrementalHashAsync(
        IEnumerable<byte[]> dataSegments,
        CancellationToken cancellationToken = default)
    {
        if (dataSegments is null)
        {
            throw new ArgumentNullException(nameof(dataSegments), "Data segments cannot be null.");
        }

        // Convert to list to ensure we can process it in a separate thread
        var segmentsList = dataSegments.ToList();

        return await Task.Run(() =>
        {
            using var hasher = Hasher.New();
            foreach (var segment in segmentsList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (segment is null)
                {
                    throw new ArgumentNullException(nameof(dataSegments), "Data segment cannot be null.");
                }

                hasher.Update(segment);
            }

            return hasher.Finalize().ToString();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Generates a Blake3-based MAC (Message Authentication Code) using a 32-byte key.
    /// </summary>
    /// <param name="data">The data to MAC.</param>
    /// <param name="key">A 32-byte key used for MAC generation.</param>
    /// <returns>A hex string representing the MAC.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key" /> is not 32 bytes in length.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="data" /> is null.</exception>
    [Obsolete("Use GenerateMacSafe instead for better error handling with the Result pattern.")]
    public static string GenerateMac(byte[] data, byte[] key)
    {
        if (key is null || key.Length != 32)
        {
            throw new ArgumentException("Key must be 256 bits (32 bytes).", nameof(key));
        }

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data), "Data cannot be null.");
        }

        try
        {
            using var hasher = Hasher.NewKeyed(key);
            hasher.Update(data);

            return hasher.Finalize().ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during Blake3 MAC generation.", ex);
        }
    }

    /// <summary>
    ///     Generates a Blake3-based MAC asynchronously for larger data.
    /// </summary>
    /// <param name="data">The data to MAC.</param>
    /// <param name="key">A 32-byte key used for MAC generation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A hex string representing the MAC.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key" /> is not 32 bytes in length.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="data" /> is null.</exception>
    [Obsolete("Use GenerateMacSafeAsync instead for better error handling with the Result pattern.")]
    public static async Task<string> GenerateMacAsync(
        byte[] data,
        byte[] key,
        CancellationToken cancellationToken = default)
    {
        if (key is null || key.Length != 32)
        {
            throw new ArgumentException("Key must be 256 bits (32 bytes).", nameof(key));
        }

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data), "Data cannot be null.");
        }

        // For small data, don't bother with async
        if (data.Length < 1000)
        {
            return GenerateMac(data, key);
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var hasher = Hasher.NewKeyed(key);
            hasher.Update(data);

            return hasher.Finalize().ToString();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Derives a new key from input keying material using Blake3.
    /// </summary>
    /// <param name="context">A byte array representing the context string for domain separation.</param>
    /// <param name="inputKeyingMaterial">The input keying material from which to derive a key.</param>
    /// <returns>A newly derived key as a byte array.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="context" /> or
    ///     <paramref name="inputKeyingMaterial" /> is null.
    /// </exception>
    [Obsolete("Use DeriveKeySafe instead for better error handling with the Result pattern.")]
    public static byte[] DeriveKey(byte[] context, byte[] inputKeyingMaterial)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context), "Context cannot be null.");
        }

        if (inputKeyingMaterial is null)
        {
            throw new ArgumentNullException(nameof(inputKeyingMaterial), "Input keying material cannot be null.");
        }

        try
        {
            using var hasher = Hasher.NewDeriveKey(context);
            hasher.Update(inputKeyingMaterial);

            return hasher.Finalize().AsSpan().ToArray();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during Blake3 key derivation.", ex);
        }
    }

    /// <summary>
    ///     Derives a new key from input keying material using Blake3 asynchronously.
    /// </summary>
    /// <param name="context">A byte array representing the context string for domain separation.</param>
    /// <param name="inputKeyingMaterial">The input keying material from which to derive a key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A newly derived key as a byte array.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="context" /> or
    ///     <paramref name="inputKeyingMaterial" /> is null.
    /// </exception>
    [Obsolete("Use DeriveKeySafeAsync instead for better error handling with the Result pattern.")]
    public static async Task<byte[]> DeriveKeyAsync(
        byte[] context,
        byte[] inputKeyingMaterial,
        CancellationToken cancellationToken = default)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context), "Context cannot be null.");
        }

        if (inputKeyingMaterial is null)
        {
            throw new ArgumentNullException(nameof(inputKeyingMaterial), "Input keying material cannot be null.");
        }

        // For small inputs, just use the synchronous method
        if (inputKeyingMaterial.Length < 1000)
        {
            return DeriveKey(context, inputKeyingMaterial);
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var hasher = Hasher.NewDeriveKey(context);
            hasher.Update(inputKeyingMaterial);

            return hasher.Finalize().AsSpan().ToArray();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Hashes the contents of the provided <see cref="Stream" /> using Blake3 in a streaming fashion.
    /// </summary>
    /// <param name="inputStream">The input stream to read and hash.</param>
    /// <returns>The final hash as a hex string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="inputStream" /> is null.</exception>
    [Obsolete("Use HashStreamSafe instead for better error handling with the Result pattern.")]
    public static string HashStream(Stream inputStream)
    {
        if (inputStream is null)
        {
            throw new ArgumentNullException(nameof(inputStream), "Input stream cannot be null.");
        }

        try
        {
            using var hasher = Hasher.New();

            // Use ArrayPool to avoid allocations for buffer
            var buffer = ArrayPool<byte>.Shared.Rent(4096); // 4KB buffer
            try
            {
                int bytesRead;
                while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hasher.Update(buffer.AsSpan(0, bytesRead));
                }

                return hasher.Finalize().ToString();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during Blake3 stream hashing.", ex);
        }
    }

    /// <summary>
    ///     Asynchronously hashes the content of a stream using Blake3.
    /// </summary>
    /// <param name="inputStream">The stream to hash.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The hash as a hex string.</returns>
    /// <exception cref="ArgumentNullException">If the stream is null.</exception>
    /// <exception cref="InvalidOperationException">If an error occurs during hashing.</exception>
    [Obsolete("Use HashStreamSafeAsync instead for better error handling with the Result pattern.")]
    public static async Task<string> HashStreamAsync(
        Stream inputStream,
        CancellationToken cancellationToken = default)
    {
        if (inputStream is null)
        {
            throw new ArgumentNullException(nameof(inputStream), "Input stream cannot be null.");
        }

        if (!inputStream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(inputStream));
        }

        try
        {
            using var hasher = Hasher.New();

            // Use ArrayPool to reuse buffer
            var buffer = ArrayPool<byte>.Shared.Rent(16384); // 16KB chunks for async
            try
            {
                int bytesRead;
                while ((bytesRead = await inputStream.ReadAsync(
                           buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    hasher.Update(buffer.AsSpan(0, bytesRead));
                }

                return hasher.Finalize().ToString();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during async Blake3 stream hashing.", ex);
        }
    }

    /// <summary>
    ///     Computes a Blake3 hash for a large file asynchronously, using chunked processing and memory-efficient operations.
    /// </summary>
    /// <param name="filePath">Path to the file to hash.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The file's hash as a hex string.</returns>
    public static async Task<Result<string, HashingError>> HashFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return Result<string, HashingError>.Failure(HashComputationError.EmptyInput);
        }

        try
        {
            if (!File.Exists(filePath))
            {
                return Result<string, HashingError>.Failure(
                    new HashComputationError($"File not found: {filePath}"));
            }

            using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                true);

            return await HashStreamSafeAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result<string, HashingError>.Failure(
                new HashComputationError("Operation was canceled"));
        }
        catch (Exception ex)
        {
            return Result<string, HashingError>.Failure(
                HashComputationError.AlgorithmError(ex.Message), ex);
        }
    }

    #endregion
}
