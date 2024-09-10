#region

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Extensions;
using DropBear.Codex.Files.Interfaces;
using DropBear.Codex.Files.Models;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Services;

/// <summary>
///     Manages file operations for the DropBear.Codex.Files library.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FileManager
{
    private readonly ILogger _logger;
    private readonly IStorageManager _storageManager;

    /// <summary>
    ///     Initializes a new instance of the FileManager class.
    /// </summary>
    /// <param name="storageManager">The storage manager implementation.</param>
    /// <exception cref="PlatformNotSupportedException">Thrown when the operating system is not Windows.</exception>
    /// <exception cref="ArgumentNullException">Thrown when storageManager is null.</exception>
    internal FileManager(IStorageManager? storageManager)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("FileManager is only supported on Windows.");
        }

        _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        _logger = LoggerFactory.Logger.ForContext<FileManager>();
        _logger.Debug("FileManager initialized with {StorageManager}", _storageManager.GetType().Name);
    }

    /// <summary>
    ///     Writes data to the specified file path.
    /// </summary>
    /// <typeparam name="T">The type of the data to write. Supported types are DropBearFile and byte[].</typeparam>
    /// <param name="data">The data to write to the file.</param>
    /// <param name="fullPath">The full path of the file.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> WriteToFileAsync<T>(T data, string fullPath)
    {
        _logger.Debug("Attempting to write {DataType} to file {FilePath}", typeof(T).Name, fullPath);

        try
        {
            var validationResult = await ValidateFilePathAsync(fullPath).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            var streamResult = await ConvertToStreamAsync(data).ConfigureAwait(false);
            if (!streamResult.IsSuccess)
            {
                _logger.Warning("Failed to convert {DataType} to stream. Error: {ErrorMessage}", typeof(T).Name,
                    streamResult.ErrorMessage);
                return Result.Failure(streamResult.ErrorMessage);
            }

            var stream = streamResult.Value;
            await using (stream.ConfigureAwait(false))
            {
                var writeResult = await _storageManager.WriteAsync(fullPath, stream).ConfigureAwait(false);
                if (writeResult.IsSuccess)
                {
                    _logger.Information("Successfully wrote {DataType} to file {FilePath}", typeof(T).Name, fullPath);
                    return Result.Success();
                }

                _logger.Warning("Failed to write to file {FilePath}. Error: {ErrorMessage}", fullPath,
                    writeResult.ErrorMessage);
                return writeResult;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error writing {DataType} to file {FilePath}", typeof(T).Name, fullPath);
            return Result.Failure($"Error writing to file: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Reads data from the specified file path.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="fullPath">The full path of the file.</param>
    /// <returns>A result containing the data read from the file or an error message.</returns>
    public async Task<Result<T>> ReadFromFileAsync<T>(string fullPath)
    {
        _logger.Debug("Attempting to read {DataType} from file {FilePath}", typeof(T).Name, fullPath);

        try
        {
            var validationResult = await ValidateFilePathAsync(fullPath).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return Result<T>.Failure(validationResult.ErrorMessage);
            }

            var streamResult = await _storageManager.ReadAsync(fullPath).ConfigureAwait(false);
            if (!streamResult.IsSuccess)
            {
                _logger.Warning("Failed to read from file {FilePath}. Error: {ErrorMessage}", fullPath,
                    streamResult.ErrorMessage);
                return Result<T>.Failure(streamResult.ErrorMessage);
            }

            var stream = streamResult.Value;
            await using (stream.ConfigureAwait(false))
            {
                return await ConvertFromStreamAsync<T>(stream).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error reading from file {FilePath}", fullPath);
            return Result<T>.Failure(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Updates data in the specified file path.
    /// </summary>
    /// <typeparam name="T">The type of the data to update. Supported types are DropBearFile and byte[].</typeparam>
    /// <param name="data">The data to update in the file.</param>
    /// <param name="fullPath">The full path of the file.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> UpdateFileAsync<T>(T data, string fullPath)
    {
        _logger.Debug("Attempting to update {DataType} in file {FilePath}", typeof(T).Name, fullPath);

        try
        {
            var validationResult = await ValidateFilePathAsync(fullPath).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            var streamResult = await ConvertToStreamAsync(data).ConfigureAwait(false);
            if (!streamResult.IsSuccess)
            {
                _logger.Warning("Failed to convert {DataType} to stream for update. Error: {ErrorMessage}",
                    typeof(T).Name, streamResult.ErrorMessage);
                return Result.Failure(streamResult.ErrorMessage);
            }

            var stream = streamResult.Value;
            await using (stream.ConfigureAwait(false))
            {
                var updateResult = await _storageManager.UpdateAsync(fullPath, stream).ConfigureAwait(false);
                if (updateResult.IsSuccess)
                {
                    _logger.Information("Successfully updated {DataType} in file {FilePath}", typeof(T).Name, fullPath);
                    return Result.Success();
                }

                _logger.Warning("Failed to update file {FilePath}. Error: {ErrorMessage}", fullPath,
                    updateResult.ErrorMessage);
                return updateResult;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error updating {DataType} in file {FilePath}", typeof(T).Name, fullPath);
            return Result.Failure($"Error updating file: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Deletes the specified file.
    /// </summary>
    /// <param name="fullPath">The full path of the file.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> DeleteFileAsync(string fullPath)
    {
        _logger.Debug("Attempting to delete file {FilePath}", fullPath);

        try
        {
            var validationResult = await ValidateFilePathAsync(fullPath).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            if (!File.Exists(fullPath))
            {
                _logger.Warning("File {FilePath} does not exist", fullPath);
                return Result.Failure("File does not exist.");
            }

            var deleteResult = await _storageManager.DeleteAsync(fullPath).ConfigureAwait(false);
            if (deleteResult.IsSuccess)
            {
                _logger.Information("Successfully deleted file {FilePath}", fullPath);
                return Result.Success();
            }

            _logger.Warning("Failed to delete file {FilePath}. Error: {ErrorMessage}", fullPath,
                deleteResult.ErrorMessage);
            return deleteResult;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error deleting file {FilePath}", fullPath);
            return Result.Failure(ex.Message, ex);
        }
    }

    private async Task<Result> ValidateFilePathAsync(string fullPath)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directoryPath))
            {
                return Result.Failure("Invalid file path.");
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                _logger.Information("Directory created successfully: {DirectoryPath}", directoryPath);
            }

            if (await HasWritePermissionOnDirAsync(directoryPath).ConfigureAwait(false))
            {
                return Result.Success();
            }

            _logger.Information("Setting write permissions on directory: {DirectoryPath}", directoryPath);
            var currentUser = WindowsIdentity.GetCurrent().Name;
            await AddDirectorySecurityAsync(directoryPath, currentUser, FileSystemRights.WriteData,
                AccessControlType.Allow).ConfigureAwait(false);

            return await HasWritePermissionOnDirAsync(directoryPath)
                .ConfigureAwait(false)
                ? Result.Success()
                : Result.Failure("Failed to set write permissions on directory.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error validating file path: {FilePath}", fullPath);
            return Result.Failure("Error validating file path.", ex);
        }
    }

    private static async Task<bool> HasWritePermissionOnDirAsync(string path)
    {
        try
        {
            var dInfo = new DirectoryInfo(path);
            var dSecurity = await Task.Run(() => dInfo.GetAccessControl()).ConfigureAwait(false);
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

    private static async Task AddDirectorySecurityAsync(string path, string account, FileSystemRights rights,
        AccessControlType controlType)
    {
        try
        {
            var dInfo = new DirectoryInfo(path);
            var dSecurity = await Task.Run(() => dInfo.GetAccessControl()).ConfigureAwait(false);
            dSecurity.AddAccessRule(new FileSystemAccessRule(account, rights,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None,
                controlType));
            await Task.Run(() => dInfo.SetAccessControl(dSecurity)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding directory security for: {DirectoryPath}", path);
            throw;
        }
    }

    private async Task<Result<Stream>> ConvertToStreamAsync<T>(T data)
    {
        return data switch
        {
            DropBearFile file => Result<Stream>.Success(await file.ToStreamAsync(_logger).ConfigureAwait(false)),
            byte[] byteArray => Result<Stream>.Success(new MemoryStream(byteArray)),
            // Add any other supported types here
            _ => Result<Stream>.Failure($"Unsupported type for write operation: {typeof(T).Name}")
        };
    }

    private async Task<Result<T>> ConvertFromStreamAsync<T>(Stream stream)
    {
        if (typeof(T) == typeof(byte[]))
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return Result<T>.Success((T)(object)ms.ToArray());
        }

        if (typeof(T) == typeof(DropBearFile))
        {
            var file = await DropBearFileExtensions.FromStreamAsync(stream, _logger).ConfigureAwait(false);
            return Result<T>.Success((T)(object)file);
        }

        // Add any other supported types here

        return Result<T>.Failure($"Unsupported type for read operation: {typeof(T).Name}");
    }
}
