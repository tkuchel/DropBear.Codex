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
public class LocalStorageManager : IStorageManager
{
    private readonly string _baseDirectory;
    private readonly ILogger? _logger;
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LocalStorageManager" /> class.
    /// </summary>
    /// <param name="memoryStreamManager">The memory stream manager for efficient memory usage.</param>
    /// <param name="logger">The logger instance for logging operations.</param>
    /// <param name="baseDirectory">The base directory for file storage.</param>
    public LocalStorageManager(
        RecyclableMemoryStreamManager memoryStreamManager,
        ILogger? logger = null,
        string baseDirectory = @"C:\Data")
    {
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        _memoryStreamManager = memoryStreamManager ?? throw new ArgumentNullException(nameof(memoryStreamManager));
        _logger = logger ?? LoggerFactory.Logger.ForContext<LocalStorageManager>();

        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
            _logger.Information("Created base directory: {BaseDirectory}", _baseDirectory);
        }
    }

    /// <summary>
    ///     Writes data to a file in the specified subdirectory.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="dataStream">The data stream to write.</param>
    /// <param name="subDirectory">The subdirectory within the base directory.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public async Task<Result> WriteAsync(string fileName, Stream dataStream, string? subDirectory = null)
    {
        var fullPath = string.Empty; // Declare fullPath outside the try block

        try
        {
            var directoryPath = Path.Combine(_baseDirectory, subDirectory ?? string.Empty);
            fullPath = Path.Combine(directoryPath, fileName);

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                _logger?.Information("Created directory: {DirectoryPath}", directoryPath);
            }

            if (dataStream is null)
            {
                throw new ArgumentNullException(nameof(dataStream), "The data stream cannot be null.");
            }

            if (dataStream.Length == 0)
            {
                return Result.Failure("Attempting to write an empty stream to local storage.");
            }

            if (!dataStream.CanSeek)
            {
                var memoryStream = _memoryStreamManager.GetStream();
                await dataStream.CopyToAsync(memoryStream).ConfigureAwait(false);
                memoryStream.Position = 0;
                dataStream = memoryStream;
            }

            dataStream.Position = 0;

            var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await using (fileStream.ConfigureAwait(false))
            {
                await dataStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }

            _logger?.Information("Successfully wrote file {FileName} to {FullPath}", fileName, fullPath);
            return Result.Success();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Error(ex, "Unauthorized access while writing file {FileName} to {FullPath}", fileName, fullPath);
            return Result.Failure("Unauthorized access.", ex);
        }
        catch (IOException ex)
        {
            _logger?.Error(ex, "IO exception while writing file {FileName} to {FullPath}", fileName, fullPath);
            return Result.Failure("IO exception occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while writing file {FileName} to {FullPath}", fileName, fullPath);
            return Result.Failure("An unexpected error occurred.", ex);
        }
    }


    /// <summary>
    ///     Reads data from a file in the specified subdirectory.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="subDirectory">The subdirectory within the base directory.</param>
    /// <returns>A <see cref="Result{T}" /> containing the data stream or an error message.</returns>
    public async Task<Result<Stream>> ReadAsync(string fileName, string? subDirectory = null)
    {
        var fullPath = string.Empty; // Declare fullPath outside the try block

        try
        {
            var directoryPath = Path.Combine(_baseDirectory, subDirectory ?? string.Empty);
            fullPath = Path.Combine(directoryPath, fileName);

            if (!File.Exists(fullPath))
            {
                _logger?.Error("File not found: {FullPath}", fullPath);
                return Result<Stream>.Failure("The specified file does not exist.");
            }

            var memoryStream = _memoryStreamManager.GetStream();

            var fileStream =
                new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await using (fileStream.ConfigureAwait(false))
            {
                await fileStream.CopyToAsync(memoryStream).ConfigureAwait(false);
            }

            memoryStream.Position = 0;
            _logger?.Information("Successfully read file {FileName} from {FullPath}", fileName, fullPath);
            return Result<Stream>.Success(memoryStream);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Error(ex, "Unauthorized access while reading file {FileName} from {FullPath}", fileName, fullPath);
            return Result<Stream>.Failure("Unauthorized access.", ex);
        }
        catch (IOException ex)
        {
            _logger?.Error(ex, "IO exception while reading file {FileName} from {FullPath}", fileName, fullPath);
            return Result<Stream>.Failure("IO exception occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while reading file {FileName} from {FullPath}", fileName, fullPath);
            return Result<Stream>.Failure("An unexpected error occurred.", ex);
        }
    }

    /// <summary>
    ///     Updates a file with new data in the specified subdirectory.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="newDataStream">The new data stream.</param>
    /// <param name="subDirectory">The subdirectory within the base directory.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public async Task<Result> UpdateAsync(string fileName, Stream newDataStream, string? subDirectory = null)
    {
        try
        {
            var directoryPath = Path.Combine(_baseDirectory, subDirectory ?? string.Empty);
            var fullPath = Path.Combine(directoryPath, fileName);

            if (!File.Exists(fullPath))
            {
                _logger?.Error("File {FileName} does not exist for update in directory {DirectoryPath}", fileName,
                    directoryPath);
                return Result.Failure("The specified file does not exist for update.");
            }

            File.Delete(fullPath);
            _logger?.Information("Deleted existing file {FileName} from {FullPath} before update", fileName, fullPath);

            return await WriteAsync(fileName, newDataStream, subDirectory).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Error(ex, "Unauthorized access while updating file {FileName} in directory {DirectoryPath}",
                fileName, subDirectory);
            return Result.Failure("Unauthorized access.", ex);
        }
        catch (IOException ex)
        {
            _logger?.Error(ex, "IO exception while updating file {FileName} in directory {DirectoryPath}", fileName,
                subDirectory);
            return Result.Failure("IO exception occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while updating file {FileName} in directory {DirectoryPath}",
                fileName,
                subDirectory);
            return Result.Failure("An unexpected error occurred.", ex);
        }
    }

    /// <summary>
    ///     Deletes a file from the specified subdirectory.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="subDirectory">The subdirectory within the base directory.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public Task<Result> DeleteAsync(string fileName, string? subDirectory = null)
    {
        try
        {
            var directoryPath = Path.Combine(_baseDirectory, subDirectory ?? string.Empty);
            var fullPath = Path.Combine(directoryPath, fileName);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger?.Information("Successfully deleted file {FileName} from {FullPath}", fileName, fullPath);
                return Task.FromResult(Result.Success());
            }

            _logger?.Error("File not found for deletion: {FullPath}", fullPath);
            return Task.FromResult(Result.Failure("The specified file does not exist."));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.Error(ex, "Unauthorized access while deleting file {FileName} from {FullPath}", fileName,
                subDirectory);
            return Task.FromResult(Result.Failure("Unauthorized access.", ex));
        }
        catch (IOException ex)
        {
            _logger?.Error(ex, "IO exception while deleting file {FileName} from {FullPath}", fileName, subDirectory);
            return Task.FromResult(Result.Failure("IO exception occurred.", ex));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "An error occurred while deleting file {FileName} from {FullPath}", fileName,
                subDirectory);
            return Task.FromResult(Result.Failure("An unexpected error occurred.", ex));
        }
    }
}
