#region

using System.Buffers;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Files.Errors;
using DropBear.Codex.Files.Interfaces;
using FluentStorage.Blobs;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Files.StorageManagers;

/// <summary>
///     Manages blob storage operations using FluentStorage's <see cref="IBlobStorage" />,
///     providing methods to write, read, update, and delete blobs in a specified container.
/// </summary>
public sealed class BlobStorageManager : IStorageManager
{
    // Optimal buffer size for blob operations
    private const int BufferSize = 81920; // 80KB buffer for blob operations
    private readonly IBlobStorage _blobStorage;
    private readonly string _containerName;
    private readonly ILogger _logger;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BlobStorageManager" /> class.
    /// </summary>
    /// <param name="blobStorage">The underlying FluentStorage <see cref="IBlobStorage" /> provider.</param>
    /// <param name="memoryStreamManager">A <see cref="RecyclableMemoryStreamManager" /> for efficient memory usage.</param>
    /// <param name="logger">An optional logger instance for logging operations.</param>
    /// <param name="containerName">The default container name in the blob storage account.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="blobStorage" /> or <paramref name="memoryStreamManager" /> is null.
    /// </exception>
    public BlobStorageManager(
        IBlobStorage blobStorage,
        RecyclableMemoryStreamManager memoryStreamManager,
        ILogger? logger = null,
        string containerName = "default-container")
    {
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _memoryStreamManager = memoryStreamManager ?? throw new ArgumentNullException(nameof(memoryStreamManager));
        _logger = logger ?? LoggerFactory.Logger.ForContext<BlobStorageManager>();
        _containerName = containerName;
    }

    /// <inheritdoc />
    public async Task<Result<Unit, StorageError>> WriteAsync(
        string identifier,
        Stream dataStream,
        CancellationToken cancellationToken = default)
    {
        if (dataStream == null)
        {
            return Result<Unit, StorageError>.Failure(
                StorageError.WriteFailed(identifier, "Data stream is null."));
        }

        if (dataStream.Length == 0)
        {
            return Result<Unit, StorageError>.Failure(
                StorageError.WriteFailed(identifier, "Attempting to write an empty stream to blob storage."));
        }

        var fullPath = GetFullPath(identifier, _containerName);
        try
        {
            var seekableStream = await GetSeekableStreamAsync(dataStream, cancellationToken).ConfigureAwait(false);
            await using (seekableStream.ConfigureAwait(false))
            {
                await _blobStorage.WriteAsync(fullPath, seekableStream, false, cancellationToken)
                    .ConfigureAwait(false);

                _logger.Information("Successfully wrote blob {BlobName} to container {ContainerName}",
                    identifier, _containerName);
                return Result<Unit, StorageError>.Success(Unit.Value);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Write operation for {BlobName} was cancelled", identifier);
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error writing blob {BlobName} to container {ContainerName}", identifier,
                _containerName);
            return Result<Unit, StorageError>.Failure(
                StorageError.WriteFailed(identifier, ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Stream, StorageError>> ReadAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(identifier, _containerName);

        try
        {
            var readStream = await _blobStorage.OpenReadAsync(fullPath, cancellationToken).ConfigureAwait(false);
            if (readStream == null)
            {
                _logger.Error("Blob not found or no access: {BlobName} in container {ContainerName}",
                    identifier, _containerName);
                return Result<Stream, StorageError>.Failure(
                    StorageError.ReadFailed(identifier, "Blob not found or no access."));
            }

            // Use memory stream with a size hint if available
            var memoryStream = readStream.Length > 0 && readStream.Length <= int.MaxValue
                ? _memoryStreamManager.GetStream(null, (int)readStream.Length)
                : _memoryStreamManager.GetStream();

            // Use ArrayPool for optimal buffer management
            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                int bytesRead;
                while ((bytesRead = await readStream.ReadAsync(
                           buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await memoryStream.WriteAsync(
                        buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                await readStream.DisposeAsync().ConfigureAwait(false);
            }

            memoryStream.Position = 0;

            _logger.Information("Successfully read blob {BlobName} from container {ContainerName}",
                identifier, _containerName);
            return Result<Stream, StorageError>.Success(memoryStream);
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Read operation for {BlobName} was cancelled", identifier);
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error reading blob {BlobName} from container {ContainerName}",
                identifier, _containerName);
            return Result<Stream, StorageError>.Failure(
                StorageError.ReadFailed(identifier, ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit, StorageError>> DeleteAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(identifier, _containerName);

        try
        {
            await _blobStorage.DeleteAsync([fullPath], cancellationToken).ConfigureAwait(false);
            _logger.Information("Successfully deleted blob {BlobName} from container {ContainerName}",
                identifier, _containerName);
            return Result<Unit, StorageError>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Delete operation for {BlobName} was cancelled", identifier);
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error deleting blob {BlobName} from container {ContainerName}",
                identifier, _containerName);
            return Result<Unit, StorageError>.Failure(
                StorageError.DeleteFailed(identifier, ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit, StorageError>> UpdateAsync(
        string identifier,
        Stream newDataStream,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(identifier, _containerName);

        try
        {
            // Check if the blob exists before attempting to update
            var exists = await _blobStorage.ExistsAsync([fullPath], cancellationToken).ConfigureAwait(false);
            if (!exists.FirstOrDefault())
            {
                _logger.Error("Blob {BlobName} does not exist in container {ContainerName}",
                    identifier, _containerName);
                return Result<Unit, StorageError>.Failure(
                    StorageError.UpdateFailed(identifier, "The specified blob does not exist."));
            }

            // Delete old blob & write new data
            await _blobStorage.DeleteAsync([fullPath], cancellationToken).ConfigureAwait(false);
            return await WriteAsync(identifier, newDataStream, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Update operation for {BlobName} was cancelled", identifier);
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error updating blob {BlobName} in container {ContainerName}",
                identifier, _containerName);
            return Result<Unit, StorageError>.Failure(
                StorageError.UpdateFailed(identifier, ex.Message), ex);
        }
    }

    #region Private Helpers

    /// <summary>
    ///     If <paramref name="inputStream" /> is non-seekable, copy it into a MemoryStream to allow repeated operations.
    /// </summary>
    private async Task<Stream> GetSeekableStreamAsync(Stream inputStream, CancellationToken cancellationToken)
    {
        if (inputStream.CanSeek)
        {
            return inputStream;
        }

        // Use memory stream with a size hint if available
        var memoryStream = inputStream.Length > 0 && inputStream.Length <= int.MaxValue
            ? _memoryStreamManager.GetStream(null, (int)inputStream.Length)
            : _memoryStreamManager.GetStream();

        // Use ArrayPool for optimal buffer management
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            int bytesRead;
            while ((bytesRead = await inputStream.ReadAsync(
                       buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await memoryStream.WriteAsync(
                    buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    ///     Builds a "full path" string used by FluentStorage for the blob, combining container and blob name.
    /// </summary>
    /// <param name="blobName">The user-specified blob identifier.</param>
    /// <param name="containerName">An optional override container name (defaults to <see cref="_containerName" />).</param>
    /// <returns>A validated full path string.</returns>
    private string GetFullPath(string blobName, string? containerName)
    {
        containerName ??= _containerName;
        var fullPath = $"{containerName}/{blobName}";
        return ValidateBlobName(fullPath);
    }

    /// <summary>
    ///     Ensures the blob name is valid (non-empty and within length limits).
    /// </summary>
    /// <param name="blobName">The combined container/blob path.</param>
    /// <returns>The validated blob name.</returns>
    /// <exception cref="ArgumentException">Thrown if blob name is invalid.</exception>
    private static string ValidateBlobName(string blobName)
    {
        if (string.IsNullOrEmpty(blobName))
        {
            throw new ArgumentException("Blob name cannot be null or empty.", nameof(blobName));
        }

        if (blobName.Length > 1024)
        {
            throw new ArgumentException("Blob name cannot exceed 1024 characters.", nameof(blobName));
        }

        return blobName;
    }

    #endregion
}
