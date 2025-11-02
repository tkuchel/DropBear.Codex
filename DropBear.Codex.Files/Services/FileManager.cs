#region

using System.Buffers;
using System.Collections.Frozen;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Async;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Files.Errors;
using DropBear.Codex.Files.Extensions;
using DropBear.Codex.Files.Interfaces;
using DropBear.Codex.Files.Models;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Services;

/// <summary>
///     Manages file operations for the DropBear.Codex.Files library.
///     Supports reading, writing, updating, and deleting files (or blobs) via an <see cref="IStorageManager" />.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FileManager
{
    private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new(
        new RecyclableMemoryStreamManager.Options
        {
            ThrowExceptionOnToArray = true,
            BlockSize = 4096 * 4, // 16KB blocks
            LargeBufferMultiple = 1024 * 1024, // 1MB increments for large buffers
            MaximumBufferSize = 1024 * 1024 * 100 // 100MB max buffer size
        });

    private readonly string _baseDirectory;
    private readonly ILogger<FileManager> _logger;
    private readonly Serilog.ILogger _serilogLogger;
    private readonly IStorageManager _storageManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FileManager" /> class.
    /// </summary>
    /// <param name="baseDirectory">
    ///     The base directory for local storage or the container name for blob storage usage.
    /// </param>
    /// <param name="storageManager">
    ///     The <see cref="IStorageManager" /> implementation (local or blob-based).
    /// </param>
    /// <param name="logger">The logger instance for FileManager.</param>
    /// <exception cref="PlatformNotSupportedException">Thrown when not running on Windows.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageManager" /> is null.</exception>
    internal FileManager(string baseDirectory, IStorageManager? storageManager, ILogger<FileManager> logger)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("FileManager is only supported on Windows.");
        }

        _baseDirectory = baseDirectory;
        _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serilogLogger = DropBear.Codex.Core.Logging.LoggerFactory.Logger.ForContext<FileManager>();
        LogFileManagerInitialized(_logger, _storageManager.GetType().Name);
    }

    /// <summary>
    ///     Writes data to the specified file (or blob) path asynchronously.
    /// </summary>
    /// <typeparam name="T">Type of data to write (e.g., <see cref="DropBearFile" /> or <c>byte[]</c>).</typeparam>
    /// <param name="data">The data to write.</param>
    /// <param name="identifier">A file path or blob name (depending on <see cref="IStorageManager" />).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A Result indicating success or failure with error details.</returns>
    public async Task<Result<Unit, FileOperationError>> WriteToFileAsync<T>(
        T data,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        LogAttemptingWrite(_logger, typeof(T).Name, identifier);

        try
        {
            // Validate path & directory permissions
            var validationResult = await ValidateFilePathAsync(identifier, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return Result<Unit, FileOperationError>.Failure(validationResult.Error!);
            }

            identifier = GetFullPath(identifier);

            // Convert the data to a stream
            var streamResult = await ConvertToStreamAsync(data, cancellationToken).ConfigureAwait(false);
            if (!streamResult.IsSuccess)
            {
                LogFailedToConvertToStream(_logger, typeof(T).Name, streamResult.Error?.Message ?? "Unknown error");
                return Result<Unit, FileOperationError>.Failure(streamResult.Error!);
            }

            // Write the stream via the storage manager
            var stream = streamResult.Value;
            await using (stream.ConfigureAwait(false))
            {
                var writeResult = await _storageManager.WriteAsync(identifier, stream, cancellationToken)
                    .ConfigureAwait(false);

                if (writeResult.IsSuccess)
                {
                    LogSuccessfullyWroteToFile(_logger, typeof(T).Name, identifier);
                    return Result<Unit, FileOperationError>.Success(Unit.Value);
                }

                LogFailedToWriteToFile(_logger, identifier, writeResult.Error?.Message ?? "Unknown error");

                return Result<Unit, FileOperationError>.Failure(
                    FileOperationError.InvalidOperation($"Failed to write to file: {writeResult.Error?.Message}"));
            }
        }
        catch (OperationCanceledException)
        {
            LogWriteOperationCancelled(_logger, identifier);
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogErrorWritingToFile(_logger, ex, typeof(T).Name, identifier);
            return Result<Unit, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Error writing to file: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Reads data of type <typeparamref name="T" /> from the specified file (or blob) path asynchronously.
    /// </summary>
    /// <typeparam name="T">Type of data to read (e.g., <see cref="DropBearFile" /> or <c>byte[]</c>).</typeparam>
    /// <param name="identifier">A file path or blob name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A Result with the read data or error details.</returns>
    public async Task<Result<T, FileOperationError>> ReadFromFileAsync<T>(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        LogAttemptingRead(_logger, typeof(T).Name, identifier);

        try
        {
            // Validate path & directory permissions
            var validationResult = await ValidateFilePathAsync(identifier, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return Result<T, FileOperationError>.Failure(validationResult.Error!);
            }

            identifier = GetFullPath(identifier);

            // Read the file via the storage manager
            var streamResult = await _storageManager.ReadAsync(identifier, cancellationToken).ConfigureAwait(false);
            if (!streamResult.IsSuccess)
            {
                LogFailedToReadFromFile(_logger, identifier, streamResult.Error?.Message ?? "Unknown error");

                return Result<T, FileOperationError>.Failure(
                    FileOperationError.ReadFailed(identifier, streamResult.Error?.Message ?? "Unknown error"));
            }

            var stream = streamResult.Value;
            await using (stream.ConfigureAwait(false))
            {
                // Convert the stream back into T
                return await ConvertFromStreamAsync<T>(stream, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            LogReadOperationCancelled(_logger, identifier);
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogErrorReadingFromFile(_logger, ex, identifier);
            return Result<T, FileOperationError>.Failure(
                FileOperationError.ReadFailed(identifier, ex.Message), ex);
        }
    }

    /// <summary>
    ///     Updates the file (or blob) at the specified path with new data.
    ///     Can overwrite the entire file or update a <see cref="DropBearFile" /> container.
    /// </summary>
    /// <typeparam name="T">Type of data to update (e.g., <see cref="DropBearFile" /> or <c>byte[]</c>).</typeparam>
    /// <param name="data">The new data to write.</param>
    /// <param name="identifier">A file path or blob name.</param>
    /// <param name="overwrite">
    ///     If <c>true</c>, overwrites the entire file.
    ///     If <c>false</c> and <typeparamref name="T" /> is a <see cref="DropBearFile" />, merges content containers.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A Result indicating success or failure with error details.</returns>
    public async Task<Result<Unit, FileOperationError>> UpdateFileAsync<T>(
        T data,
        string identifier,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        LogAttemptingUpdate(_logger, typeof(T).Name, identifier);

        try
        {
            // Validate path & directory permissions
            var validationResult = await ValidateFilePathAsync(identifier, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return Result<Unit, FileOperationError>.Failure(validationResult.Error!);
            }

            identifier = GetFullPath(identifier);

            // If not overwriting and type is DropBearFile, attempt a partial update
            DropBearFile? existingFile = null;
            if (!overwrite && typeof(T) == typeof(DropBearFile))
            {
                // Read the existing file
                var readResult = await ReadFromFileAsync<DropBearFile>(identifier, cancellationToken)
                    .ConfigureAwait(false);
                if (readResult.IsSuccess)
                {
                    existingFile = readResult.Value;
                }
                else
                {
                    LogNoExistingFileFound(_logger, identifier, readResult.Error?.Message ?? "Unknown error");
                }
            }

            // Merge or remove content containers (if DropBearFile)
            if (data is DropBearFile dropBearFile && existingFile != null)
            {
                foreach (var container in dropBearFile.ContentContainers)
                {
                    var existingContainer = existingFile.ContentContainers
                        .FirstOrDefault(c => c.Equals(container));

                    if (existingContainer == null)
                    {
                        // Add a new content container
                        existingFile.ContentContainers.Add(container);
                        LogAddedNewContentContainer(_logger);
                    }
                    else if (container.Data == null)
                    {
                        // Remove this container if data is null
                        if (existingFile.ContentContainers.Count == 1)
                        {
                            return Result<Unit, FileOperationError>.Failure(
                                FileOperationError.InvalidOperation(
                                    "Cannot remove the last content container from the file."));
                        }

                        existingFile.ContentContainers.Remove(existingContainer);
                        LogRemovedContentContainer(_logger);
                    }
                    else
                    {
                        // Update existing container
                        existingContainer.Data = container.Data;
                        existingContainer.SetHash(container.Hash);
                        LogUpdatedExistingContentContainer(_logger);
                    }
                }
            }

            // Convert updated object to stream
            Result<Stream, FileOperationError> streamResult;
            if (existingFile == null)
            {
                streamResult = await ConvertToStreamAsync(data, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                streamResult = await ConvertToStreamAsync(existingFile, cancellationToken).ConfigureAwait(false);
            }

            if (!streamResult.IsSuccess)
            {
                LogFailedToConvertToStreamForUpdate(_logger, typeof(T).Name, streamResult.Error?.Message ?? "Unknown error");

                return Result<Unit, FileOperationError>.Failure(streamResult.Error!);
            }

            var stream = streamResult.Value;
            await using (stream.ConfigureAwait(false))
            {
                // Use storage manager to update the file
                var updateResult = await _storageManager.UpdateAsync(identifier, stream, cancellationToken)
                    .ConfigureAwait(false);

                if (updateResult.IsSuccess)
                {
                    LogSuccessfullyUpdatedFile(_logger, typeof(T).Name, identifier);
                    return Result<Unit, FileOperationError>.Success(Unit.Value);
                }

                LogFailedToUpdateFile(_logger, identifier, updateResult.Error?.Message ?? "Unknown error");

                return Result<Unit, FileOperationError>.Failure(
                    FileOperationError.UpdateFailed(identifier, updateResult.Error?.Message ?? "Unknown error"));
            }
        }
        catch (OperationCanceledException)
        {
            LogUpdateOperationCancelled(_logger, identifier);
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogErrorUpdatingFile(_logger, ex, typeof(T).Name, identifier);
            return Result<Unit, FileOperationError>.Failure(
                FileOperationError.UpdateFailed(identifier, ex.Message), ex);
        }
    }

    /// <summary>
    ///     Deletes an entire file (or blob) or a single <see cref="ContentContainer" /> from the file.
    /// </summary>
    /// <param name="identifier">A file path or blob name.</param>
    /// <param name="deleteFile">If <c>true</c>, deletes the entire file.</param>
    /// <param name="containerToDelete">
    ///     If specified, deletes only that container instead of the entire file (unless <paramref name="deleteFile" /> is
    ///     <c>true</c>).
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A Result indicating success or failure with error details.</returns>
    public async Task<Result<Unit, FileOperationError>> DeleteFileAsync(
        string identifier,
        bool deleteFile = true,
        ContentContainer? containerToDelete = null,
        CancellationToken cancellationToken = default)
    {
        LogAttemptingDelete(_logger, identifier);

        try
        {
            // Validate path & directory permissions
            var validationResult = await ValidateFilePathAsync(identifier, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return Result<Unit, FileOperationError>.Failure(validationResult.Error!);
            }

            identifier = GetFullPath(identifier);

            // If deleting the entire file, just call storage manager
            if (deleteFile || containerToDelete == null)
            {
                var deleteResult =
                    await _storageManager.DeleteAsync(identifier, cancellationToken).ConfigureAwait(false);
                if (deleteResult.IsSuccess)
                {
                    LogSuccessfullyDeletedFile(_logger, identifier);
                    return Result<Unit, FileOperationError>.Success(Unit.Value);
                }

                LogFailedToDeleteFile(_logger, identifier, deleteResult.Error?.Message ?? "Unknown error");

                return Result<Unit, FileOperationError>.Failure(
                    FileOperationError.DeleteFailed(identifier, deleteResult.Error?.Message ?? "Unknown error"));
            }

            // If containerToDelete is specified, remove only that container from the file
            var readResult = await ReadFromFileAsync<DropBearFile>(identifier, cancellationToken).ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                return Result<Unit, FileOperationError>.Failure(
                    FileOperationError.ReadFailed(identifier, readResult.Error?.Message ?? "Unknown error"));
            }

            var dropBearFile = readResult.Value!;

            // Ensure we do not remove the last container
            if (dropBearFile.ContentContainers.Count == 1 && containerToDelete != null)
            {
                return Result<Unit, FileOperationError>.Failure(
                    FileOperationError.InvalidOperation("Cannot delete the last content container in the file."));
            }

            var existingContainer = dropBearFile.ContentContainers
                .FirstOrDefault(c => c.Equals(containerToDelete));

            if (existingContainer != null)
            {
                dropBearFile.ContentContainers.Remove(existingContainer);
                LogDeletedContentContainer(_logger, identifier);

                // Update the file with the new container list
                return await UpdateFileAsync(dropBearFile, identifier, true, cancellationToken).ConfigureAwait(false);
            }

            return Result<Unit, FileOperationError>.Failure(
                FileOperationError.InvalidOperation("Content container not found."));
        }
        catch (OperationCanceledException)
        {
            LogDeleteOperationCancelled(_logger, identifier);
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogErrorDeletingFile(_logger, ex, identifier);
            return Result<Unit, FileOperationError>.Failure(
                FileOperationError.DeleteFailed(identifier, ex.Message), ex);
        }
    }

    /// <summary>
    ///     Retrieves a specific <see cref="ContentContainer" /> from a <see cref="DropBearFile" /> by content type.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to search.</param>
    /// <param name="contentType">The content type to match.</param>
    /// <returns>
    ///     A <see cref="Result{T, TError}" /> containing the matching <see cref="ContentContainer" /> (or null if not found)
    ///     on success, or a <see cref="FileOperationError" /> if an error occurred during the search.
    /// </returns>
    public Result<ContentContainer?, FileOperationError> GetContainerByContentType(DropBearFile file, string contentType)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(contentType);

        try
        {
            var container = file.ContentContainers
                .FirstOrDefault(c => c.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase));
            return Result<ContentContainer?, FileOperationError>.Success(container);
        }
        catch (Exception ex)
        {
            LogErrorGettingContainerByContentType(_logger, ex, contentType);
            return Result<ContentContainer?, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Error retrieving container by content type: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Retrieves a specific <see cref="ContentContainer" /> from a <see cref="DropBearFile" /> by hash.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to search.</param>
    /// <param name="hash">The hash to match.</param>
    /// <returns>
    ///     A <see cref="Result{T, TError}" /> containing the matching <see cref="ContentContainer" /> (or null if not found)
    ///     on success, or a <see cref="FileOperationError" /> if an error occurred during the search.
    /// </returns>
    public Result<ContentContainer?, FileOperationError> GetContainerByHash(DropBearFile file, string hash)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(hash);

        try
        {
            var container = file.ContentContainers
                .FirstOrDefault(c => string.Equals(c.Hash, hash, StringComparison.Ordinal));
            return Result<ContentContainer?, FileOperationError>.Success(container);
        }
        catch (Exception ex)
        {
            LogErrorGettingContainerByHash(_logger, ex, hash);
            return Result<ContentContainer?, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Error retrieving container by hash: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Lists all distinct content types in the <see cref="DropBearFile" />.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> whose containers are listed.</param>
    /// <returns>
    ///     A <see cref="Result{T, TError}" /> containing a frozen list of content types
    ///     on success, or a <see cref="FileOperationError" /> if an error occurred.
    /// </returns>
    public Result<IReadOnlyList<string>, FileOperationError> ListContainerTypes(DropBearFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        try
        {
            // Use frozen collections for read-only lists (.NET 8 feature)
            var types = file.ContentContainers
                .Select(c => c.ContentType)
                .ToFrozenSet(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Result<IReadOnlyList<string>, FileOperationError>.Success(types);
        }
        catch (Exception ex)
        {
            LogErrorListingContainerTypes(_logger, ex);
            return Result<IReadOnlyList<string>, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Error listing container types: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Adds or updates a single <see cref="ContentContainer" /> in the <see cref="DropBearFile" />.
    ///     If it exists, updates data; otherwise adds a new container.
    /// </summary>
    /// <param name="file">The target <see cref="DropBearFile" />.</param>
    /// <param name="newContainer">The <see cref="ContentContainer" /> to add or update.</param>
    /// <returns>A Result indicating success or failure with error details.</returns>
    public Result<Unit, FileOperationError> AddOrUpdateContainer(
        DropBearFile file,
        ContentContainer newContainer)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(newContainer);

        try
        {
            var existingContainer = file.ContentContainers.FirstOrDefault(c => c.Equals(newContainer));
            if (existingContainer != null)
            {
                existingContainer.Data = newContainer.Data;
                existingContainer.SetHash(newContainer.Hash);
                LogUpdatedExistingContentContainer(_logger);
                return Result<Unit, FileOperationError>.Success(Unit.Value);
            }

            file.ContentContainers.Add(newContainer);
            LogAddedNewContentContainer(_logger);
            return Result<Unit, FileOperationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            LogErrorAddingOrUpdatingContainer(_logger, ex);
            return Result<Unit, FileOperationError>.Failure(
                FileOperationError.InvalidOperation("Error adding or updating container."), ex);
        }
    }

    /// <summary>
    ///     Removes a <see cref="ContentContainer" /> from the <see cref="DropBearFile" /> by content type,
    ///     ensuring not to remove the last container.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to modify.</param>
    /// <param name="contentType">The content type of the container to remove.</param>
    /// <returns>A Result indicating success or failure with error details.</returns>
    public Result<Unit, FileOperationError> RemoveContainerByContentType(
        DropBearFile file,
        string contentType)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(contentType);

        try
        {
            var getResult = GetContainerByContentType(file, contentType);
            if (!getResult.IsSuccess)
            {
                return Result<Unit, FileOperationError>.Failure(getResult.Error, getResult.Exception);
            }

            var containerToRemove = getResult.Value;
            if (containerToRemove == null)
            {
                return Result<Unit, FileOperationError>.Failure(
                    FileOperationError.InvalidOperation("Content container not found."));
            }

            if (file.ContentContainers.Count == 1)
            {
                return Result<Unit, FileOperationError>.Failure(
                    FileOperationError.InvalidOperation("Cannot remove the last content container."));
            }

            file.ContentContainers.Remove(containerToRemove);
            LogRemovedContainerByContentType(_logger, contentType);
            return Result<Unit, FileOperationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            LogErrorRemovingContainerByContentType(_logger, ex, contentType);
            return Result<Unit, FileOperationError>.Failure(
                FileOperationError.InvalidOperation("Error removing content container."), ex);
        }
    }

    /// <summary>
    ///     Counts the total number of <see cref="ContentContainer" /> objects in the <see cref="DropBearFile" />.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to count containers in.</param>
    /// <returns>
    ///     A <see cref="Result{T, TError}" /> containing the count of containers
    ///     on success, or a <see cref="FileOperationError" /> if an error occurred.
    /// </returns>
    public Result<int, FileOperationError> CountContainers(DropBearFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        try
        {
            var count = file.ContentContainers.Count;
            return Result<int, FileOperationError>.Success(count);
        }
        catch (Exception ex)
        {
            LogErrorCountingContainers(_logger, ex);
            return Result<int, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Error counting containers: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Counts the number of <see cref="ContentContainer" /> objects in the <see cref="DropBearFile" />
    ///     that match a specific content type.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to search.</param>
    /// <param name="contentType">The content type to match.</param>
    /// <returns>
    ///     A <see cref="Result{T, TError}" /> containing the count of matching containers
    ///     on success, or a <see cref="FileOperationError" /> if an error occurred.
    /// </returns>
    public Result<int, FileOperationError> CountContainersByType(DropBearFile file, string contentType)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(contentType);

        try
        {
            var count = file.ContentContainers.Count(c =>
                c.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase));
            return Result<int, FileOperationError>.Success(count);
        }
        catch (Exception ex)
        {
            LogErrorCountingContainersByType(_logger, ex, contentType);
            return Result<int, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Error counting containers by type: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Replaces an old <see cref="ContentContainer" /> in the <see cref="DropBearFile" /> with a new one.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to modify.</param>
    /// <param name="oldContainer">The old container to remove.</param>
    /// <param name="newContainer">The new container to add in its place.</param>
    /// <returns>A Result indicating success or failure with error details.</returns>
    public Result<Unit, FileOperationError> ReplaceContainer(
        DropBearFile file,
        ContentContainer oldContainer,
        ContentContainer newContainer)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(oldContainer);
        ArgumentNullException.ThrowIfNull(newContainer);

        try
        {
            var existingContainer = file.ContentContainers.FirstOrDefault(c => c.Equals(oldContainer));
            if (existingContainer == null)
            {
                return Result<Unit, FileOperationError>.Failure(
                    FileOperationError.InvalidOperation("Content container not found."));
            }

            file.ContentContainers.Remove(existingContainer);
            file.ContentContainers.Add(newContainer);

            LogReplacedContentContainer(_logger);
            return Result<Unit, FileOperationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            LogErrorReplacingContainer(_logger, ex);
            return Result<Unit, FileOperationError>.Failure(
                FileOperationError.InvalidOperation("Error replacing content container."), ex);
        }
    }

    #region Async Enumerable Streaming Methods

    /// <summary>
    ///     Streams <see cref="ContentContainer" /> objects from a <see cref="DropBearFile" /> asynchronously,
    ///     optionally filtered by a predicate. Useful for processing large files with many containers
    ///     without loading all containers into memory at once.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to stream containers from.</param>
    /// <param name="predicateAsync">Optional async predicate to filter containers. If null, all containers are streamed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="AsyncEnumerableResult{T, TError}" /> that yields matching containers incrementally.</returns>
    /// <remarks>
    ///     This method is preferred over loading all containers into memory when working with files
    ///     containing hundreds or thousands of containers. Containers are yielded as they match the predicate.
    /// </remarks>
    public AsyncEnumerableResult<ContentContainer, FileOperationError> StreamContainersAsync(
        DropBearFile file,
        Func<ContentContainer, ValueTask<bool>>? predicateAsync = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        async IAsyncEnumerable<ContentContainer> StreamInternal(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var container in file.ContentContainers)
            {
                ct.ThrowIfCancellationRequested();

                if (predicateAsync == null || await predicateAsync(container).ConfigureAwait(false))
                {
                    yield return container;
                }
            }
        }

        return AsyncEnumerableResult<ContentContainer, FileOperationError>.Success(StreamInternal(cancellationToken));
    }

    /// <summary>
    ///     Streams distinct content types from a <see cref="DropBearFile" /> asynchronously.
    ///     This is more memory-efficient than <see cref="ListContainerTypes" /> for files with many containers.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> whose container types are streamed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="AsyncEnumerableResult{T, TError}" /> that yields distinct content types incrementally.</returns>
    /// <remarks>
    ///     Content types are yielded in the order they are first encountered in the container collection.
    ///     Duplicates are automatically filtered out using case-insensitive comparison.
    /// </remarks>
    public AsyncEnumerableResult<string, FileOperationError> StreamContainerTypesAsync(
        DropBearFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        async IAsyncEnumerable<string> StreamInternal(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            var seenTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var container in file.ContentContainers)
            {
                ct.ThrowIfCancellationRequested();

                if (seenTypes.Add(container.ContentType))
                {
                    yield return container.ContentType;
                }

                // Allow cooperative cancellation
                await Task.Yield();
            }
        }

        return AsyncEnumerableResult<string, FileOperationError>.Success(StreamInternal(cancellationToken));
    }

    /// <summary>
    ///     Streams <see cref="ContentContainer" /> objects that match a specific content type.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to search.</param>
    /// <param name="contentType">The content type to match (case-insensitive).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="AsyncEnumerableResult{T, TError}" /> that yields matching containers.</returns>
    /// <remarks>
    ///     Useful for processing containers of a specific type in large files without materializing
    ///     the entire collection. Matching is case-insensitive.
    /// </remarks>
    public AsyncEnumerableResult<ContentContainer, FileOperationError> StreamContainersByTypeAsync(
        DropBearFile file,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(contentType);

        async IAsyncEnumerable<ContentContainer> StreamInternal(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var container in file.ContentContainers)
            {
                ct.ThrowIfCancellationRequested();

                if (container.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase))
                {
                    yield return container;
                }

                // Allow cooperative cancellation
                await Task.Yield();
            }
        }

        return AsyncEnumerableResult<ContentContainer, FileOperationError>.Success(StreamInternal(cancellationToken));
    }

    /// <summary>
    ///     Streams deserialized data from <see cref="ContentContainer" /> objects of a specific type.
    ///     This method performs lazy deserialization, processing each container only when enumerated.
    /// </summary>
    /// <typeparam name="T">The type to deserialize container data into.</typeparam>
    /// <param name="file">The <see cref="DropBearFile" /> to stream from.</param>
    /// <param name="contentType">The content type to match (case-insensitive).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="AsyncEnumerableResult{T, TError}" /> that yields deserialized data incrementally.</returns>
    /// <remarks>
    ///     This method is significantly more memory-efficient than deserializing all containers upfront.
    ///     Deserialization errors for individual containers are logged but do not stop the stream;
    ///     failed containers are skipped.
    /// </remarks>
    public AsyncEnumerableResult<T, FileOperationError> StreamContainerDataAsync<T>(
        DropBearFile file,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(contentType);

        async IAsyncEnumerable<T> StreamInternal(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var container in file.ContentContainers)
            {
                ct.ThrowIfCancellationRequested();

                if (!container.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Attempt to deserialize the container data
                var deserializeResult = await container.GetDataAsync<T>(ct).ConfigureAwait(false);
                if (deserializeResult.IsSuccess && deserializeResult.Value is not null)
                {
                    yield return deserializeResult.Value;
                }
                else
                {
                    LogFailedToDeserializeContainer(_logger, contentType, deserializeResult.Error?.Message ?? "Unknown error");
                    // Skip this container and continue streaming
                }

                // Allow cooperative cancellation
                await Task.Yield();
            }
        }

        return AsyncEnumerableResult<T, FileOperationError>.Success(StreamInternal(cancellationToken));
    }

    #endregion

    #region Private/Helper Methods

    // Builds full path within base directory
    /// <summary>
    /// Gets the full path for a file, ensuring it stays within the base directory (prevents path traversal).
    /// SECURITY: Validates against directory traversal attacks (../, absolute paths, etc.)
    /// </summary>
    private string GetFullPath(string path)
    {
        // Get the absolute path
        string fullPath;
        if (Path.IsPathRooted(path) && path.StartsWith(_baseDirectory, StringComparison.Ordinal))
        {
            // Already a full path within base directory
            fullPath = Path.GetFullPath(path);
        }
        else
        {
            // Combine with base directory and resolve
            fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, path));
        }

        // SECURITY: Ensure resolved path is still within base directory
        // This prevents path traversal attacks like "../../etc/passwd"
        if (!fullPath.StartsWith(_baseDirectory, StringComparison.Ordinal))
        {
            LogPathTraversalAttemptBlocked(_logger, path, fullPath, _baseDirectory);

            throw new UnauthorizedAccessException(
                $"Path traversal detected: '{path}' attempts to access outside base directory");
        }

        return fullPath;
    }

    // Validates directory existence and permissions
    private async Task<Result<Unit, FileOperationError>> ValidateFilePathAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directoryPath = Path.GetDirectoryName(GetFullPath(path));
            if (string.IsNullOrEmpty(directoryPath))
            {
                return Result<Unit, FileOperationError>.Failure(
                    FileOperationError.InvalidOperation("Invalid file path."));
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                LogDirectoryCreatedSuccessfully(_logger, directoryPath);
            }

            if (await HasWritePermissionOnDirAsync(directoryPath, cancellationToken).ConfigureAwait(false))
            {
                return Result<Unit, FileOperationError>.Success(Unit.Value);
            }

            LogSettingWritePermissions(_logger, directoryPath);
            var currentUser = WindowsIdentity.GetCurrent().Name;
            await AddDirectorySecurityAsync(directoryPath, currentUser, FileSystemRights.WriteData,
                AccessControlType.Allow, cancellationToken).ConfigureAwait(false);

            return await HasWritePermissionOnDirAsync(directoryPath, cancellationToken).ConfigureAwait(false)
                ? Result<Unit, FileOperationError>.Success(Unit.Value)
                : Result<Unit, FileOperationError>.Failure(
                    FileOperationError.AccessDenied(directoryPath));
        }
        catch (OperationCanceledException)
        {
            LogFilePathValidationCancelled(_logger, path);
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            LogErrorValidatingFilePath(_logger, ex, path);
            return Result<Unit, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Error validating file path: {ex.Message}"), ex);
        }
    }

    private static async Task<bool> HasWritePermissionOnDirAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dInfo = new DirectoryInfo(path);
            var dSecurity = await Task.Run(() => dInfo.GetAccessControl(), cancellationToken).ConfigureAwait(false);
            var rules = dSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier));

            return rules.Cast<FileSystemAccessRule>().Any(rule =>
                (rule.FileSystemRights & FileSystemRights.WriteData) == FileSystemRights.WriteData
                && rule.AccessControlType == AccessControlType.Allow);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking write permission on directory: {DirectoryPath}", path);
            return false;
        }
    }

    private static async Task AddDirectorySecurityAsync(
        string path,
        string account,
        FileSystemRights rights,
        AccessControlType controlType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dInfo = new DirectoryInfo(path);
            var dSecurity = await Task.Run(() => dInfo.GetAccessControl(), cancellationToken).ConfigureAwait(false);

            dSecurity.AddAccessRule(new FileSystemAccessRule(
                account,
                rights,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                controlType));

            await Task.Run(() => dInfo.SetAccessControl(dSecurity), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding directory security for: {DirectoryPath}", path);
            throw;
        }
    }

    private async Task<Result<Stream, FileOperationError>> ConvertToStreamAsync<T>(
        T data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (data is DropBearFile file)
            {
                // If it's a DropBearFile, serialize asynchronously to a stream
                var result = await file.ToStreamAsync(_serilogLogger, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    return Result<Stream, FileOperationError>.Success(result.Value);
                }

                return Result<Stream, FileOperationError>.Failure(result.Error);
            }

            if (data is byte[] byteArray)
            {
                // If it's a byte array, create a RecyclableMemoryStream
                var memoryStream = MemoryStreamManager.GetStream();
                await memoryStream.WriteAsync(byteArray, cancellationToken).ConfigureAwait(false);
                memoryStream.Position = 0;
                return Result<Stream, FileOperationError>.Success(memoryStream);
            }

            // Unsupported type
            return Result<Stream, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Unsupported type for write operation: {typeof(T).Name}"));
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            return Result<Stream, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Error converting {typeof(T).Name} to stream: {ex.Message}"), ex);
        }
    }

    private async Task<Result<T, FileOperationError>> ConvertFromStreamAsync<T>(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (typeof(T) == typeof(byte[]))
            {
                // Copy stream to a byte array using ArrayPool for efficiency
                using var ms = new MemoryStream();

                var buffer = ArrayPool<byte>.Shared.Rent(81920);
                try
                {
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                               .ConfigureAwait(false)) > 0)
                    {
                        await ms.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                return Result<T, FileOperationError>.Success((T)(object)ms.ToArray());
            }

            if (typeof(T) == typeof(DropBearFile))
            {
                // Use extension method to deserialize
                var result = await DropBearFileExtensions.FromStreamAsync(stream, _serilogLogger, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    return Result<T, FileOperationError>.Success((T)(object)result.Value!);
                }

                return Result<T, FileOperationError>.Failure(result.Error!);
            }

            // Add other supported types here
            return Result<T, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Unsupported type for read operation: {typeof(T).Name}"));
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            return Result<T, FileOperationError>.Failure(
                FileOperationError.InvalidOperation($"Error converting stream to {typeof(T).Name}: {ex.Message}"), ex);
        }
    }

    #endregion

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileManager initialized with {StorageManager}")]
    static partial void LogFileManagerInitialized(ILogger<FileManager> logger, string storageManager);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Attempting to write {DataType} to file {FilePath}")]
    static partial void LogAttemptingWrite(ILogger<FileManager> logger, string dataType, string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to convert {DataType} to stream. Error: {ErrorMessage}")]
    static partial void LogFailedToConvertToStream(ILogger<FileManager> logger, string dataType, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully wrote {DataType} to file {FilePath}")]
    static partial void LogSuccessfullyWroteToFile(ILogger<FileManager> logger, string dataType, string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to write to file {FilePath}. Error: {ErrorMessage}")]
    static partial void LogFailedToWriteToFile(ILogger<FileManager> logger, string filePath, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Write operation for {FilePath} was cancelled")]
    static partial void LogWriteOperationCancelled(ILogger<FileManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error writing {DataType} to file {FilePath}")]
    static partial void LogErrorWritingToFile(ILogger<FileManager> logger, Exception ex, string dataType, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Attempting to read {DataType} from file {FilePath}")]
    static partial void LogAttemptingRead(ILogger<FileManager> logger, string dataType, string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read from file {FilePath}. Error: {ErrorMessage}")]
    static partial void LogFailedToReadFromFile(ILogger<FileManager> logger, string filePath, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Read operation for {FilePath} was cancelled")]
    static partial void LogReadOperationCancelled(ILogger<FileManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error reading from file {FilePath}")]
    static partial void LogErrorReadingFromFile(ILogger<FileManager> logger, Exception ex, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Attempting to update {DataType} in file {FilePath}")]
    static partial void LogAttemptingUpdate(ILogger<FileManager> logger, string dataType, string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No existing file found at {FilePath}. Error: {ErrorMessage}")]
    static partial void LogNoExistingFileFound(ILogger<FileManager> logger, string filePath, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Added new content container to DropBearFile.")]
    static partial void LogAddedNewContentContainer(ILogger<FileManager> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed content container from DropBearFile.")]
    static partial void LogRemovedContentContainer(ILogger<FileManager> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated existing content container in DropBearFile.")]
    static partial void LogUpdatedExistingContentContainer(ILogger<FileManager> logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to convert {DataType} to stream for update. Error: {ErrorMessage}")]
    static partial void LogFailedToConvertToStreamForUpdate(ILogger<FileManager> logger, string dataType, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully updated {DataType} in file {FilePath}")]
    static partial void LogSuccessfullyUpdatedFile(ILogger<FileManager> logger, string dataType, string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to update file {FilePath}. Error: {ErrorMessage}")]
    static partial void LogFailedToUpdateFile(ILogger<FileManager> logger, string filePath, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update operation for {FilePath} was cancelled")]
    static partial void LogUpdateOperationCancelled(ILogger<FileManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error updating {DataType} in file {FilePath}")]
    static partial void LogErrorUpdatingFile(ILogger<FileManager> logger, Exception ex, string dataType, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Attempting to delete file {FilePath} or specific content container")]
    static partial void LogAttemptingDelete(ILogger<FileManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully deleted file {FilePath}")]
    static partial void LogSuccessfullyDeletedFile(ILogger<FileManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete file {FilePath}. Error: {ErrorMessage}")]
    static partial void LogFailedToDeleteFile(ILogger<FileManager> logger, string filePath, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted content container from file {FilePath}")]
    static partial void LogDeletedContentContainer(ILogger<FileManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Delete operation for {FilePath} was cancelled")]
    static partial void LogDeleteOperationCancelled(ILogger<FileManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error deleting file {FilePath}")]
    static partial void LogErrorDeletingFile(ILogger<FileManager> logger, Exception ex, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error getting content container by content type: {ContentType}")]
    static partial void LogErrorGettingContainerByContentType(ILogger<FileManager> logger, Exception ex, string contentType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error getting content container by hash: {Hash}")]
    static partial void LogErrorGettingContainerByHash(ILogger<FileManager> logger, Exception ex, string hash);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error listing content container types.")]
    static partial void LogErrorListingContainerTypes(ILogger<FileManager> logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error adding or updating content container.")]
    static partial void LogErrorAddingOrUpdatingContainer(ILogger<FileManager> logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed content container with content type: {ContentType}")]
    static partial void LogRemovedContainerByContentType(ILogger<FileManager> logger, string contentType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error removing content container by content type: {ContentType}")]
    static partial void LogErrorRemovingContainerByContentType(ILogger<FileManager> logger, Exception ex, string contentType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error counting content containers.")]
    static partial void LogErrorCountingContainers(ILogger<FileManager> logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error counting content containers by content type: {ContentType}")]
    static partial void LogErrorCountingContainersByType(ILogger<FileManager> logger, Exception ex, string contentType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replaced content container in DropBearFile.")]
    static partial void LogReplacedContentContainer(ILogger<FileManager> logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error replacing content container.")]
    static partial void LogErrorReplacingContainer(ILogger<FileManager> logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize container of type {ContentType}. Error: {ErrorMessage}")]
    static partial void LogFailedToDeserializeContainer(ILogger<FileManager> logger, string contentType, string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Path traversal attempt blocked: {RequestedPath} resolved to {FullPath}, base: {BaseDirectory}")]
    static partial void LogPathTraversalAttemptBlocked(ILogger<FileManager> logger, string requestedPath, string fullPath, string baseDirectory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Directory created successfully: {DirectoryPath}")]
    static partial void LogDirectoryCreatedSuccessfully(ILogger<FileManager> logger, string directoryPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Setting write permissions on directory: {DirectoryPath}")]
    static partial void LogSettingWritePermissions(ILogger<FileManager> logger, string directoryPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "File path validation was cancelled: {FilePath}")]
    static partial void LogFilePathValidationCancelled(ILogger<FileManager> logger, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error validating file path: {FilePath}")]
    static partial void LogErrorValidatingFilePath(ILogger<FileManager> logger, Exception ex, string filePath);

    #endregion
}
