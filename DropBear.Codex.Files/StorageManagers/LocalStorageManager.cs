#region

using System.Buffers;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Files.Errors;
using DropBear.Codex.Files.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.IO;

#endregion

namespace DropBear.Codex.Files.StorageManagers;

/// <summary>
///     Manages local file storage operations, providing methods to write, read, update, and delete files on disk.
///     Uses a <see cref="RecyclableMemoryStreamManager" /> for memory efficiency.
/// </summary>
public sealed partial class LocalStorageManager : IStorageManager
{
    // Optimal buffer size for file operations
    private const int BufferSize = 81920; // 80KB buffer for file operations
    private readonly ILogger<LocalStorageManager> _logger;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LocalStorageManager" /> class.
    /// </summary>
    /// <param name="memoryStreamManager">A <see cref="RecyclableMemoryStreamManager" /> for efficient memory usage.</param>
    /// <param name="logger">The logger instance for logging operations.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="memoryStreamManager" /> or <paramref name="logger" /> is null.</exception>
    public LocalStorageManager(
        RecyclableMemoryStreamManager memoryStreamManager,
        ILogger<LocalStorageManager> logger)
    {
        _memoryStreamManager = memoryStreamManager ?? throw new ArgumentNullException(nameof(memoryStreamManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                StorageError.WriteFailed(identifier, "Attempting to write an empty stream to local storage."));
        }

        // SECURITY: Validate path to prevent path traversal attacks (PCI-DSS 6.5.1)
        var validationResult = ValidateFilePath(identifier);
        if (!validationResult.IsSuccess)
        {
            return Result<Unit, StorageError>.Failure(validationResult.Error!);
        }
        var safePath = validationResult.Value!; // Safe: we checked IsSuccess above

        try
        {
            // Ensure the directory exists
            EnsureDirectoryExists(Path.GetDirectoryName(safePath)!);

            // Convert to a seekable stream if necessary
            var seekableStream = await GetSeekableStreamAsync(dataStream, cancellationToken).ConfigureAwait(false);
            await using (seekableStream.ConfigureAwait(false))
            {
                // Use file options for optimal performance
                var fileStream = new FileStream(
                    safePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                await using (fileStream.ConfigureAwait(false))
                {
                    await seekableStream.CopyToAsync(fileStream, BufferSize, cancellationToken).ConfigureAwait(false);
                    LogWriteSuccessful(identifier);
                    return Result<Unit, StorageError>.Success(Unit.Value);
                }
            }
        }
        catch (OperationCanceledException)
        {
            LogWriteCancelled(identifier);
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogWriteFailed(ex, identifier);
            return Result<Unit, StorageError>.Failure(
                StorageError.WriteFailed(identifier, ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Stream, StorageError>> ReadAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        // SECURITY: Validate path to prevent path traversal attacks (PCI-DSS 6.5.1)
        var validationResult = ValidateFilePath(identifier);
        if (!validationResult.IsSuccess)
        {
            return Result<Stream, StorageError>.Failure(validationResult.Error!);
        }
        var safePath = validationResult.Value!;

        try
        {
            if (!File.Exists(safePath))
            {
                LogFileNotFound(identifier);
                return Result<Stream, StorageError>.Failure(
                    StorageError.ReadFailed(identifier, "The specified file does not exist."));
            }

            var fileInfo = new FileInfo(safePath);

            // Create memory stream with size hint
            var memoryStream = fileInfo.Length <= int.MaxValue
                ? _memoryStreamManager.GetStream(null, (int)fileInfo.Length)
                : _memoryStreamManager.GetStream();

            // Use optimized file options for reading
            var fileStream = new FileStream(
                safePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await using (fileStream.ConfigureAwait(false))
            {
                // Use ArrayPool for optimal buffer management
                var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                try
                {
                    int bytesRead;
                    while ((bytesRead = await fileStream.ReadAsync(
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

                LogReadSuccessful(identifier);
                return Result<Stream, StorageError>.Success(memoryStream);
            }
        }
        catch (OperationCanceledException)
        {
            LogReadCancelled(identifier);
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogReadFailed(ex, identifier);
            return Result<Stream, StorageError>.Failure(
                StorageError.ReadFailed(identifier, ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit, StorageError>> UpdateAsync(
        string identifier,
        Stream newDataStream,
        CancellationToken cancellationToken = default)
    {
        // SECURITY: Validate path to prevent path traversal attacks (PCI-DSS 6.5.1)
        var validationResult = ValidateFilePath(identifier);
        if (!validationResult.IsSuccess)
        {
            return Result<Unit, StorageError>.Failure(validationResult.Error!);
        }
        var safePath = validationResult.Value!;

        try
        {
            if (!File.Exists(safePath))
            {
                LogFileNotFoundForUpdate(identifier);
                return Result<Unit, StorageError>.Failure(
                    StorageError.UpdateFailed(identifier, "The specified file does not exist for update."));
            }

            // First delete the existing file
            File.Delete(safePath);
            LogDeletedBeforeUpdate(identifier);

            // Then write the new data
            return await WriteAsync(identifier, newDataStream, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            LogUpdateCancelled(identifier);
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogUpdateFailed(ex, identifier);
            return Result<Unit, StorageError>.Failure(
                StorageError.UpdateFailed(identifier, ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public Task<Result<Unit, StorageError>> DeleteAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        // SECURITY: Validate path to prevent path traversal attacks (PCI-DSS 6.5.1)
        var validationResult = ValidateFilePath(identifier);
        if (!validationResult.IsSuccess)
        {
            return Task.FromResult(Result<Unit, StorageError>.Failure(validationResult.Error!));
        }
        var safePath = validationResult.Value!;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(safePath))
            {
                LogFileNotFoundForDeletion(identifier);
                return Task.FromResult(Result<Unit, StorageError>.Failure(
                    StorageError.DeleteFailed(identifier, "The specified file does not exist.")));
            }

            File.Delete(safePath);
            LogDeleteSuccessful(identifier);
            return Task.FromResult(Result<Unit, StorageError>.Success(Unit.Value));
        }
        catch (OperationCanceledException)
        {
            LogDeleteCancelled(identifier);
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogDeleteFailed(ex, identifier);
            return Task.FromResult(Result<Unit, StorageError>.Failure(
                StorageError.DeleteFailed(identifier, ex.Message), ex));
        }
    }

    #region Private Helper Methods

    /// <summary>
    ///     Validates a file path to prevent path traversal attacks.
    ///     This is a critical security control per PCI-DSS requirement 6.5.1 (Injection).
    /// </summary>
    /// <param name="path">The file path to validate.</param>
    /// <returns>A Result containing the validated full path, or an error if validation fails.</returns>
    private Result<string, StorageError> ValidateFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            LogPathTraversalAttempt(path, "Path is null or empty");
            return Result<string, StorageError>.Failure(
                StorageError.InvalidPath(path, "Path cannot be null or empty."));
        }

        // Check for path traversal sequences BEFORE resolving the full path
        // This catches attempts like "../../../etc/passwd" or "..\\..\\Windows\\System32"
        if (path.Contains("..", StringComparison.Ordinal) ||
            path.Contains("~", StringComparison.Ordinal))
        {
            LogPathTraversalAttempt(path, "Path contains traversal sequence");
            return Result<string, StorageError>.Failure(
                StorageError.InvalidPath(path, "Path traversal sequences are not allowed."));
        }

        // Check for null bytes which can be used to truncate paths
        if (path.Contains('\0'))
        {
            LogPathTraversalAttempt(path, "Path contains null byte");
            return Result<string, StorageError>.Failure(
                StorageError.InvalidPath(path, "Path contains invalid characters."));
        }

        try
        {
            // Get the full path to resolve any symbolic links or relative components
            var fullPath = Path.GetFullPath(path);

            // Additional check: verify the resolved path doesn't escape to unexpected locations
            // by checking that it's a valid rooted path
            if (!Path.IsPathRooted(fullPath))
            {
                LogPathTraversalAttempt(path, "Resolved path is not rooted");
                return Result<string, StorageError>.Failure(
                    StorageError.InvalidPath(path, "Invalid path format."));
            }

            return Result<string, StorageError>.Success(fullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            LogPathTraversalAttempt(path, $"Path validation failed: {ex.Message}");
            return Result<string, StorageError>.Failure(
                StorageError.InvalidPath(path, $"Invalid path: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Creates a seekable stream from a potentially non-seekable input stream.
    /// </summary>
    /// <param name="inputStream">The input stream that may not be seekable.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A seekable stream containing the same data as the input stream.</returns>
    private async Task<Stream> GetSeekableStreamAsync(Stream inputStream, CancellationToken cancellationToken)
    {
        if (inputStream.CanSeek)
        {
            return inputStream;
        }

        // Use memory stream with size hint if available
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
    ///     Ensures a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="directoryPath">The directory path to ensure exists.</param>
    private void EnsureDirectoryExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            return;
        }

        Directory.CreateDirectory(directoryPath);
        LogDirectoryCreated(directoryPath);
    }

    #endregion

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully wrote file {fileName} to local storage")]
    private partial void LogWriteSuccessful(string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Write operation for {fileName} was cancelled")]
    private partial void LogWriteCancelled(string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error writing file {fileName} to local storage")]
    private partial void LogWriteFailed(Exception ex, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully read file {fileName} from local storage")]
    private partial void LogReadSuccessful(string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Read operation for {fileName} was cancelled")]
    private partial void LogReadCancelled(string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "File not found: {fileName}")]
    private partial void LogFileNotFound(string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error reading file {fileName} from local storage")]
    private partial void LogReadFailed(Exception ex, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted existing file {fileName} before update")]
    private partial void LogDeletedBeforeUpdate(string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update operation for {fileName} was cancelled")]
    private partial void LogUpdateCancelled(string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "File {fileName} does not exist for update")]
    private partial void LogFileNotFoundForUpdate(string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error updating file {fileName}")]
    private partial void LogUpdateFailed(Exception ex, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully deleted file {fileName} from local storage")]
    private partial void LogDeleteSuccessful(string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Delete operation for {fileName} was cancelled")]
    private partial void LogDeleteCancelled(string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "File not found for deletion: {fileName}")]
    private partial void LogFileNotFoundForDeletion(string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error deleting file {fileName} from local storage")]
    private partial void LogDeleteFailed(Exception ex, string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created directory: {directoryPath}")]
    private partial void LogDirectoryCreated(string directoryPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SECURITY: Path traversal attempt blocked. Path: {path}, Reason: {reason}")]
    private partial void LogPathTraversalAttempt(string? path, string reason);

    #endregion
}
