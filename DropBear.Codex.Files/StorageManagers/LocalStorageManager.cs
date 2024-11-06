#region

using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Files.Interfaces;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Files.StorageManagers;

/// <summary>
///     Manages local storage operations, providing methods to write, read, update, and delete files.
/// </summary>
public sealed class LocalStorageManager : IStorageManager
{
    private readonly ILogger _logger;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LocalStorageManager" /> class.
    /// </summary>
    /// <param name="memoryStreamManager">The memory stream manager for efficient memory usage.</param>
    /// <param name="logger">The logger instance for logging operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when baseDirectory or memoryStreamManager is null.</exception>
    public LocalStorageManager(
        RecyclableMemoryStreamManager memoryStreamManager,
        ILogger? logger = null)
    {
        _memoryStreamManager = memoryStreamManager ?? throw new ArgumentNullException(nameof(memoryStreamManager));
        _logger = logger ?? LoggerFactory.Logger.ForContext<LocalStorageManager>();
    }

    /// <inheritdoc />
    public async Task<Result> WriteAsync(string identifier, Stream dataStream,
        CancellationToken cancellationToken = default)
    {
        if (dataStream == null)
        {
            throw new ArgumentNullException(nameof(dataStream));
        }

        if (dataStream.Length == 0)
        {
            return Result.Failure("Attempting to write an empty stream to local storage.");
        }

        try
        {
            EnsureDirectoryExists(Path.GetDirectoryName(identifier)!);

            var seekableStream = await GetSeekableStreamAsync(dataStream, cancellationToken).ConfigureAwait(false);
            await using (seekableStream.ConfigureAwait(false))
            {
                var fileStream =
                    new FileStream(identifier, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await using (fileStream.ConfigureAwait(false))
                {
                    await seekableStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

                    _logger.Information("Successfully wrote file {FileName} to {FullPath}", identifier, identifier);
                    return Result.Success();
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or OperationCanceledException)
        {
            _logger.Error(ex, "Error writing file {FileName} to {FullPath}", identifier, identifier);
            return Result.Failure(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Stream>> ReadAsync(string identifier,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(identifier))
            {
                _logger.Error("File not found: {FullPath}", identifier);
                return Result<Stream>.Failure("The specified file does not exist.");
            }

            var memoryStream = _memoryStreamManager.GetStream();
            var fileStream =
                new FileStream(identifier, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await using (fileStream.ConfigureAwait(false))
            {
                await fileStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);

                memoryStream.Position = 0;
                _logger.Information("Successfully read file {FileName} from {FullPath}", identifier, identifier);
                return Result<Stream>.Success(memoryStream);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or OperationCanceledException)
        {
            _logger.Error(ex, "Error reading file {FileName} from {FullPath}", identifier, identifier);
            return Result<Stream>.Failure(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(string identifier, Stream newDataStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(identifier))
            {
                _logger.Error("File {FileName} does not exist for update in {FullPath}", identifier, identifier);
                return Result.Failure("The specified file does not exist for update.");
            }

            File.Delete(identifier);
            _logger.Information("Deleted existing file {FileName} from {FullPath} before update", identifier,
                identifier);

            return await WriteAsync(identifier, newDataStream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or OperationCanceledException)
        {
            _logger.Error(ex, "Error updating file {FileName} in {FullPath}", identifier, identifier);
            return Result.Failure(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(string identifier,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(identifier))
            {
                _logger.Error("File not found for deletion: {FullPath}", identifier);
                return Task.FromResult(Result.Failure("The specified file does not exist."));
            }

            File.Delete(identifier);
            _logger.Information("Successfully deleted file {FileName} from {FullPath}", identifier, identifier);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error deleting file {FileName} from {FullPath}", identifier, identifier);
            return Task.FromResult(Result.Failure(ex.Message, ex));
        }
    }

    // Private methods remain the same, but update GetSeekableStreamAsync to include CancellationToken
    private async Task<Stream> GetSeekableStreamAsync(Stream inputStream, CancellationToken cancellationToken)
    {
        if (inputStream.CanSeek)
        {
            return inputStream;
        }

        var memoryStream = _memoryStreamManager.GetStream();
        await inputStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;
        return memoryStream;
    }

    private void EnsureDirectoryExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            return;
        }

        Directory.CreateDirectory(directoryPath);
        _logger.Information("Created directory: {DirectoryPath}", directoryPath);
    }
}
