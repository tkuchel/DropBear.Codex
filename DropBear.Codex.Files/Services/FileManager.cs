#region

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Enums;
using DropBear.Codex.Files.Extensions;
using DropBear.Codex.Files.Interfaces;
using DropBear.Codex.Files.Models;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Services;

[SupportedOSPlatform("windows")]
public class FileManager
{
    private readonly ILogger _logger;
    private readonly IStorageManager _storageManager;
    private readonly StorageStrategy _storageStrategy;

    internal FileManager(StorageStrategy storageStrategy, IStorageManager storageManager)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("FileManager is only supported on Windows.");
        }

        _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        _storageStrategy = storageStrategy;

        _logger = LoggerFactory.Logger.ForContext<FileManager>();
    }

    #region Public Methods

    /// <summary>
    ///     Writes data to the specified file path.
    /// </summary>
    /// <typeparam name="T">The type of the data to write.</typeparam>
    /// <param name="data">The data to write to the file.</param>
    /// <param name="fullPath">The full path of the file.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> WriteToFileAsync<T>(T data, string fullPath)
    {
        try
        {
            var validationResult = ValidateFilePath(fullPath);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            Stream? stream = data switch
            {
                DropBearFile file => await file.ToStreamAsync(_logger).ConfigureAwait(false),
                byte[] byteArray => new MemoryStream(byteArray),
                _ => null
            };

            if (stream is null)
            {
                return Result.Failure("Unsupported type for write operation.");
            }

            await using (stream.ConfigureAwait(false))
            {
                var writeResult = await _storageManager.WriteAsync(fullPath, stream).ConfigureAwait(false);
                return writeResult.IsSuccess ? Result.Success() : writeResult;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, "Unauthorized access while writing to file {FilePath}", fullPath);
            return Result.Failure("Unauthorized access.", ex);
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "IO exception while writing to file {FilePath}", fullPath);
            return Result.Failure("IO exception occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred while writing to file {FilePath}", fullPath);
            return Result.Failure("An unexpected error occurred.", ex);
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
        try
        {
            var validationResult = ValidateFilePath(fullPath);
            if (!validationResult.IsSuccess)
            {
                return Result<T>.Failure(validationResult.ErrorMessage);
            }

            var streamResult = await _storageManager.ReadAsync(fullPath).ConfigureAwait(false);
            if (!streamResult.IsSuccess)
            {
                return Result<T>.Failure(streamResult.ErrorMessage);
            }

            var stream = streamResult.Value;
            await using (stream.ConfigureAwait(false))
            {
                if (typeof(T) == typeof(byte[]))
                {
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms).ConfigureAwait(false);
                    return Result<T>.Success((T)(object)ms.ToArray());
                }

                if (typeof(T) != typeof(DropBearFile))
                {
                    return Result<T>.Failure("Unsupported type for read operation.");
                }

                var file = await DropBearFileExtensions.FromStreamAsync(stream, _logger).ConfigureAwait(false);
                return Result<T>.Success((T)(object)file);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, "Unauthorized access while reading from file {FilePath}", fullPath);
            return Result<T>.Failure("Unauthorized access.", ex);
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "IO exception while reading from file {FilePath}", fullPath);
            return Result<T>.Failure("IO exception occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred while reading from file {FilePath}", fullPath);
            return Result<T>.Failure("An unexpected error occurred.", ex);
        }
    }

    /// <summary>
    ///     Updates data in the specified file path.
    /// </summary>
    /// <typeparam name="T">The type of the data to update.</typeparam>
    /// <param name="data">The data to update in the file.</param>
    /// <param name="fullPath">The full path of the file.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> UpdateFileAsync<T>(T data, string fullPath)
    {
        try
        {
            var validationResult = ValidateFilePath(fullPath);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            Stream? stream = data switch
            {
                DropBearFile file => await file.ToStreamAsync(_logger).ConfigureAwait(false),
                byte[] byteArray => new MemoryStream(byteArray),
                _ => null
            };

            if (stream is null)
            {
                return Result.Failure("Unsupported type for update operation.");
            }

            await using (stream.ConfigureAwait(false))
            {
                var updateResult = await _storageManager.UpdateAsync(fullPath, stream).ConfigureAwait(false);
                return updateResult.IsSuccess ? Result.Success() : updateResult;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, "Unauthorized access while updating file {FilePath}", fullPath);
            return Result.Failure("Unauthorized access.", ex);
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "IO exception while updating file {FilePath}", fullPath);
            return Result.Failure("IO exception occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred while updating file {FilePath}", fullPath);
            return Result.Failure("An unexpected error occurred.", ex);
        }
    }

    /// <summary>
    ///     Deletes the specified file.
    /// </summary>
    /// <param name="fullPath">The full path of the file.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> DeleteFileAsync(string fullPath)
    {
        try
        {
            var validationResult = ValidateFilePath(fullPath);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            var deleteResult = await _storageManager.DeleteAsync(fullPath).ConfigureAwait(false);
            return deleteResult.IsSuccess ? Result.Success() : deleteResult;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, "Unauthorized access while deleting file {FilePath}", fullPath);
            return Result.Failure("Unauthorized access.", ex);
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "IO exception while deleting file {FilePath}", fullPath);
            return Result.Failure("IO exception occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred while deleting file {FilePath}", fullPath);
            return Result.Failure("An unexpected error occurred.", ex);
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Validates the file path for correctness and checks write permissions.
    /// </summary>
    /// <param name="fullPath">The full path of the file.</param>
    /// <returns>A result indicating whether the file path is valid.</returns>
    private Result ValidateFilePath(string fullPath)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(fullPath);
            if (directoryPath is null)
            {
                return Result.Failure("Invalid file path.");
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                _logger.Information("Directory created successfully: {DirectoryPath}", directoryPath);
            }

            if (HasWritePermissionOnDir(directoryPath))
            {
                return Result.Success();
            }

            _logger.Information("Setting write permissions on directory: {DirectoryPath}", directoryPath);
            var currentUser = WindowsIdentity.GetCurrent().Name;
            AddDirectorySecurity(directoryPath, currentUser, FileSystemRights.WriteData, AccessControlType.Allow);

            return HasWritePermissionOnDir(directoryPath)
                ? Result.Success()
                : Result.Failure("Failed to set write permissions on directory.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error validating file path: {FilePath}", fullPath);
            return Result.Failure("Error validating file path.", ex);
        }
    }

    private static bool HasWritePermissionOnDir(string path)
    {
        try
        {
            var dInfo = new DirectoryInfo(path);
            var dSecurity = dInfo.GetAccessControl();
            var rules = dSecurity.GetAccessRules(true, true, typeof(SecurityIdentifier));
            foreach (FileSystemAccessRule rule in rules)
            {
                if ((rule.FileSystemRights & FileSystemRights.WriteData) is FileSystemRights.WriteData)
                {
                    continue;
                }

                if (rule.AccessControlType is AccessControlType.Allow)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking write permission on directory: {DirectoryPath}", path);
            return false;
        }
    }

    private static void AddDirectorySecurity(string path, string account, FileSystemRights rights,
        AccessControlType controlType)
    {
        try
        {
            var dInfo = new DirectoryInfo(path);
            var dSecurity = dInfo.GetAccessControl();
            dSecurity.AddAccessRule(new FileSystemAccessRule(account, rights,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None,
                controlType));
            dInfo.SetAccessControl(dSecurity);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding directory security for: {DirectoryPath}", path);
            throw;
        }
    }

    #endregion
}
