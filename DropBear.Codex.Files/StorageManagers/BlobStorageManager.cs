#region

using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Interfaces;
using FluentStorage.Blobs;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Files.StorageManagers;

/// <summary>
///     Manages blob storage operations, providing methods to write, read, update, and delete blobs.
/// </summary>
public sealed class BlobStorageManager : IStorageManager
{
    private readonly IBlobStorage _blobStorage;
    private readonly string _defaultContainerName;
    private readonly ILogger? _logger;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BlobStorageManager" /> class.
    /// </summary>
    /// <param name="blobStorage">The blob storage provider.</param>
    /// <param name="memoryStreamManager">The memory stream manager for efficient memory usage.</param>
    /// <param name="logger">The logger instance for logging operations.</param>
    /// <param name="defaultContainerName">The default container name for blob storage.</param>
    public BlobStorageManager(
        IBlobStorage blobStorage,
        RecyclableMemoryStreamManager memoryStreamManager,
        ILogger? logger = null,
        string defaultContainerName = "default-container")
    {
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _memoryStreamManager = memoryStreamManager ?? throw new ArgumentNullException(nameof(memoryStreamManager));
        _logger = logger ?? LoggerFactory.Logger.ForContext<BlobStorageManager>();
        _defaultContainerName = defaultContainerName;
    }

    /// <summary>
    ///     Writes data to the specified blob in the given container.
    /// </summary>
    /// <param name="blobName">The name of the blob.</param>
    /// <param name="dataStream">The data stream to write.</param>
    /// <param name="containerName">The name of the container. Defaults to the configured default container.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public async Task<Result> WriteAsync(string blobName, Stream dataStream, string? containerName = null)
    {
        try
        {
            containerName ??= _defaultContainerName;
            var fullPath = $"{containerName}/{blobName}";

            fullPath = ValidateBlobName(fullPath);

            if (dataStream is null)
            {
                throw new ArgumentNullException(nameof(dataStream), "The data stream cannot be null.");
            }

            if (dataStream.Length == 0)
            {
                return Result.Failure("Attempting to write an empty stream to blob storage.");
            }

            if (!dataStream.CanSeek)
            {
                var memoryStream = _memoryStreamManager.GetStream();
                await dataStream.CopyToAsync(memoryStream).ConfigureAwait(false);
                memoryStream.Position = 0;
                dataStream = memoryStream;
            }

            dataStream.Position = 0;

            await _blobStorage.WriteAsync(fullPath, dataStream, false, CancellationToken.None).ConfigureAwait(false);
            _logger?.Information("Successfully wrote blob {BlobName} to container {ContainerName}", blobName,
                containerName);
            return Result.Success();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Error(ex, "Unauthorized access while writing blob {BlobName} to container {ContainerName}",
                blobName, containerName);
            return Result.Failure("Unauthorized access.", ex);
        }
        catch (IOException ex)
        {
            _logger?.Error(ex, "IO exception while writing blob {BlobName} to container {ContainerName}", blobName,
                containerName);
            return Result.Failure("IO exception occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while writing blob {BlobName} to container {ContainerName}",
                blobName, containerName);
            return Result.Failure("An unexpected error occurred.", ex);
        }
    }

    /// <summary>
    ///     Reads data from the specified blob in the given container.
    /// </summary>
    /// <param name="blobName">The name of the blob.</param>
    /// <param name="containerName">The name of the container. Defaults to the configured default container.</param>
    /// <returns>A <see cref="Result{T}" /> containing the data stream or an error message.</returns>
    public async Task<Result<Stream>> ReadAsync(string blobName, string? containerName = null)
    {
        try
        {
            containerName ??= _defaultContainerName;
            var fullPath = $"{containerName}/{blobName}";

            fullPath = ValidateBlobName(fullPath);

            var stream = _memoryStreamManager.GetStream();
            var readStream = await _blobStorage.OpenReadAsync(fullPath, CancellationToken.None).ConfigureAwait(false);

            if (readStream is null)
            {
                _logger?.Error("Blob not found or no access: {BlobName} in container {ContainerName}", blobName,
                    containerName);
                return Result<Stream>.Failure("Blob not found or no access.");
            }

            await readStream.CopyToAsync(stream).ConfigureAwait(false);
            stream.Position = 0;
            _logger?.Information("Successfully read blob {BlobName} from container {ContainerName}", blobName,
                containerName);
            return Result<Stream>.Success(stream);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Error(ex, "Unauthorized access while reading blob {BlobName} from container {ContainerName}",
                blobName, containerName);
            return Result<Stream>.Failure("Unauthorized access.", ex);
        }
        catch (IOException ex)
        {
            _logger?.Error(ex, "IO exception while reading blob {BlobName} from container {ContainerName}", blobName,
                containerName);
            return Result<Stream>.Failure("IO exception occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while reading blob {BlobName} from container {ContainerName}",
                blobName, containerName);
            return Result<Stream>.Failure("An unexpected error occurred.", ex);
        }
    }

    /// <summary>
    ///     Updates the specified blob with new data.
    /// </summary>
    /// <param name="blobName">The name of the blob.</param>
    /// <param name="newDataStream">The new data stream.</param>
    /// <param name="containerName">The name of the container. Defaults to the configured default container.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public async Task<Result> UpdateAsync(string blobName, Stream newDataStream, string? containerName = null)
    {
        try
        {
            containerName ??= _defaultContainerName;
            var fullPath = $"{containerName}/{blobName}";

            var exists = await _blobStorage.ExistsAsync(new[] { fullPath }, CancellationToken.None)
                .ConfigureAwait(false);
            if (!exists.FirstOrDefault())
            {
                _logger?.Error("Blob {BlobName} does not exist in container {ContainerName}", blobName,
                    containerName);
                return Result.Failure("The specified blob does not exist.");
            }

            await _blobStorage.DeleteAsync(new[] { fullPath }, CancellationToken.None).ConfigureAwait(false);
            await WriteAsync(blobName, newDataStream, containerName).ConfigureAwait(false);

            _logger?.Information("Successfully updated blob {BlobName} in container {ContainerName}", blobName,
                containerName);
            return Result.Success();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Error(ex, "Unauthorized access while updating blob {BlobName} in container {ContainerName}",
                blobName, containerName);
            return Result.Failure("Unauthorized access.", ex);
        }
        catch (IOException ex)
        {
            _logger?.Error(ex, "IO exception while updating blob {BlobName} in container {ContainerName}", blobName,
                containerName);
            return Result.Failure("IO exception occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while updating blob {BlobName} in container {ContainerName}",
                blobName, containerName);
            return Result.Failure("An unexpected error occurred.", ex);
        }
    }

    /// <summary>
    ///     Deletes the specified blob from the given container.
    /// </summary>
    /// <param name="blobName">The name of the blob.</param>
    /// <param name="containerName">The name of the container. Defaults to the configured default container.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public async Task<Result> DeleteAsync(string blobName, string? containerName = null)
    {
        try
        {
            containerName ??= _defaultContainerName;
            var fullPath = $"{containerName}/{blobName}";

            await _blobStorage.DeleteAsync(new[] { fullPath }, CancellationToken.None).ConfigureAwait(false);
            _logger?.Information("Successfully deleted blob {BlobName} from container {ContainerName}", blobName,
                containerName);
            return Result.Success();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Error(ex, "Unauthorized access while deleting blob {BlobName} from container {ContainerName}",
                blobName, containerName);
            return Result.Failure("Unauthorized access.", ex);
        }
        catch (IOException ex)
        {
            _logger?.Error(ex, "IO exception while deleting blob {BlobName} from container {ContainerName}", blobName,
                containerName);
            return Result.Failure("IO exception occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while deleting blob {BlobName} from container {ContainerName}",
                blobName, containerName);
            return Result.Failure("An unexpected error occurred.", ex);
        }
    }

    /// <summary>
    ///     Validates the blob name, ensuring it is not null, empty, or too long.
    /// </summary>
    /// <param name="blobName">The name of the blob.</param>
    /// <returns>The validated blob name.</returns>
    /// <exception cref="ArgumentException">Thrown when the blob name is invalid.</exception>
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
}
