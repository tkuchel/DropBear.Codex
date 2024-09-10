#region

using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
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
    private readonly string _baseDirectory;
    private readonly ILogger _logger;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LocalStorageManager" /> class.
    /// </summary>
    /// <param name="memoryStreamManager">The memory stream manager for efficient memory usage.</param>
    /// <param name="logger">The logger instance for logging operations.</param>
    /// <param name="baseDirectory">The base directory for file storage.</param>
    /// <exception cref="ArgumentNullException">Thrown when baseDirectory or memoryStreamManager is null.</exception>
    public LocalStorageManager(
        RecyclableMemoryStreamManager memoryStreamManager,
        ILogger? logger = null,
        string baseDirectory = @"C:\Data")
    {
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        _memoryStreamManager = memoryStreamManager ?? throw new ArgumentNullException(nameof(memoryStreamManager));
        _logger = logger ?? LoggerFactory.Logger.ForContext<LocalStorageManager>();

        EnsureBaseDirectoryExists();
    }

    /// <inheritdoc />
    public async Task<Result> WriteAsync(string identifier, Stream dataStream, string? subDirectory = null,
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

        var fullPath = GetFullPath(identifier, subDirectory);

        try
        {
            EnsureDirectoryExists(Path.GetDirectoryName(fullPath)!);

            var seekableStream = await GetSeekableStreamAsync(dataStream, cancellationToken).ConfigureAwait(false);
            await using (seekableStream.ConfigureAwait(false))
            {
                var fileStream =
                    new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await using (fileStream.ConfigureAwait(false))
                {
                    await seekableStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

                    _logger.Information("Successfully wrote file {FileName} to {FullPath}", identifier, fullPath);
                    return Result.Success();
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or OperationCanceledException)
        {
            _logger.Error(ex, "Error writing file {FileName} to {FullPath}", identifier, fullPath);
            return Result.Failure(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Stream>> ReadAsync(string identifier, string? subDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(identifier, subDirectory);

        try
        {
            if (!File.Exists(fullPath))
            {
                _logger.Error("File not found: {FullPath}", fullPath);
                return Result<Stream>.Failure("The specified file does not exist.");
            }

            var memoryStream = _memoryStreamManager.GetStream();
            var fileStream =
                new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await using (fileStream.ConfigureAwait(false))
            {
                await fileStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);

                memoryStream.Position = 0;
                _logger.Information("Successfully read file {FileName} from {FullPath}", identifier, fullPath);
                return Result<Stream>.Success(memoryStream);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or OperationCanceledException)
        {
            _logger.Error(ex, "Error reading file {FileName} from {FullPath}", identifier, fullPath);
            return Result<Stream>.Failure(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(string identifier, Stream newDataStream, string? subDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(identifier, subDirectory);

        try
        {
            if (!File.Exists(fullPath))
            {
                _logger.Error("File {FileName} does not exist for update in {FullPath}", identifier, fullPath);
                return Result.Failure("The specified file does not exist for update.");
            }

            File.Delete(fullPath);
            _logger.Information("Deleted existing file {FileName} from {FullPath} before update", identifier, fullPath);

            return await WriteAsync(identifier, newDataStream, subDirectory, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or OperationCanceledException)
        {
            _logger.Error(ex, "Error updating file {FileName} in {FullPath}", identifier, fullPath);
            return Result.Failure(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(string identifier, string? subDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(identifier, subDirectory);

        try
        {
            if (!File.Exists(fullPath))
            {
                _logger.Error("File not found for deletion: {FullPath}", fullPath);
                return Task.FromResult(Result.Failure("The specified file does not exist."));
            }

            File.Delete(fullPath);
            _logger.Information("Successfully deleted file {FileName} from {FullPath}", identifier, fullPath);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error deleting file {FileName} from {FullPath}", identifier, fullPath);
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

    private void EnsureBaseDirectoryExists()
    {
        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
            _logger.Information("Created base directory: {BaseDirectory}", _baseDirectory);
        }
    }

    private void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            _logger.Information("Created directory: {DirectoryPath}", directoryPath);
        }
    }

    private string GetFullPath(string fileName, string? subDirectory)
    {
        var directoryPath = Path.Combine(_baseDirectory, subDirectory ?? string.Empty);
        return Path.Combine(directoryPath, fileName);
    }
}
