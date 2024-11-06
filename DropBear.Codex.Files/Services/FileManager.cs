#region

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
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
    private readonly string _baseDirectory;
    private readonly ILogger _logger;
    private readonly IStorageManager _storageManager;

    /// <summary>
    ///     Initializes a new instance of the FileManager class.
    /// </summary>
    /// <param name="baseDirectory">The base directory for local storage or the container name for blob storage.</param>
    /// <param name="storageManager">The storage manager implementation.</param>
    /// <exception cref="PlatformNotSupportedException">Thrown when the operating system is not Windows.</exception>
    /// <exception cref="ArgumentNullException">Thrown when storageManager is null.</exception>
    internal FileManager(string baseDirectory, IStorageManager? storageManager)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("FileManager is only supported on Windows.");
        }

        _baseDirectory = baseDirectory;
        _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        _logger = LoggerFactory.Logger.ForContext<FileManager>();
        _logger.Debug("FileManager initialized with {StorageManager}", _storageManager.GetType().Name);
    }

    /// <summary>
    ///     Writes data to the specified file path.
    /// </summary>
    /// <typeparam name="T">The type of the data to write. Supported types are DropBearFile and byte[].</typeparam>
    /// <param name="data">The data to write to the file.</param>
    /// <param name="identifier">The path of the file or blob name for blob storage.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> WriteToFileAsync<T>(T data, string identifier)
    {
        _logger.Debug("Attempting to write {DataType} to file {FilePath}", typeof(T).Name, identifier);

        try
        {
            var validationResult = await ValidateFilePathAsync(identifier).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            identifier = GetFullPath(identifier);
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
                var writeResult = await _storageManager.WriteAsync(identifier, stream).ConfigureAwait(false);
                if (writeResult.IsSuccess)
                {
                    _logger.Information("Successfully wrote {DataType} to file {FilePath}", typeof(T).Name, identifier);
                    return Result.Success();
                }

                _logger.Warning("Failed to write to file {FilePath}. Error: {ErrorMessage}", identifier,
                    writeResult.ErrorMessage);
                return writeResult;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error writing {DataType} to file {FilePath}", typeof(T).Name, identifier);
            return Result.Failure($"Error writing to file: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Reads data from the specified file path.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="identifier">The path of the file or blob name for blob storage.</param>
    /// <returns>A result containing the data read from the file or an error message.</returns>
    public async Task<Result<T>> ReadFromFileAsync<T>(string identifier)
    {
        _logger.Debug("Attempting to read {DataType} from file {FilePath}", typeof(T).Name, identifier);

        try
        {
            var validationResult = await ValidateFilePathAsync(identifier).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return Result<T>.Failure(validationResult.ErrorMessage);
            }

            identifier = GetFullPath(identifier);
            var streamResult = await _storageManager.ReadAsync(identifier).ConfigureAwait(false);
            if (!streamResult.IsSuccess)
            {
                _logger.Warning("Failed to read from file {FilePath}. Error: {ErrorMessage}", identifier,
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
            _logger.Error(ex, "Error reading from file {FilePath}", identifier);
            return Result<T>.Failure(ex.Message, ex);
        }
    }

    /// <summary>
    ///     Updates data in the specified file path.
    /// </summary>
    /// <typeparam name="T">The type of the data to update. Supported types are DropBearFile and byte[].</typeparam>
    /// <param name="data">The data to update in the file.</param>
    /// <param name="identifier">The path of the file or blob name for blob storage.</param>
    /// <param name="overwrite">Whether to overwrite the existing file or just update the content container.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> UpdateFileAsync<T>(T data, string identifier, bool overwrite = false)
    {
        _logger.Debug("Attempting to update {DataType} in file {FilePath}", typeof(T).Name, identifier);

        try
        {
            // Validate the file path
            var validationResult = await ValidateFilePathAsync(identifier).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            identifier = GetFullPath(identifier);

            // Read the existing file (if it exists) if not overwriting
            DropBearFile? existingFile = null;
            if (!overwrite && typeof(T) == typeof(DropBearFile))
            {
                var readResult = await ReadFromFileAsync<DropBearFile>(identifier).ConfigureAwait(false);
                if (readResult.IsSuccess)
                {
                    existingFile = readResult.Value;
                }
                else
                {
                    _logger.Warning("No existing file found for {FilePath}. Error: {ErrorMessage}", identifier,
                        readResult.ErrorMessage);
                }
            }

            // Check if the content should be added, updated, or removed
            if (data is DropBearFile dropBearFile && existingFile != null)
            {
                foreach (var container in dropBearFile.ContentContainers)
                {
                    var existingContainer = existingFile.ContentContainers.FirstOrDefault(c => c.Equals(container));

                    if (existingContainer == null)
                    {
                        // Adding new content container
                        existingFile.ContentContainers.Add(container);
                        _logger.Information("Added new content container to DropBearFile.");
                    }
                    else if (container.Data == null)
                    {
                        // Remove content container if data is null
                        if (existingFile.ContentContainers.Count == 1)
                        {
                            throw new InvalidOperationException(
                                "Cannot remove the last content container from the file.");
                        }

                        existingFile.ContentContainers.Remove(existingContainer);
                        _logger.Information("Removed content container from DropBearFile.");
                    }
                    else
                    {
                        // Update existing content container
                        existingContainer.Data = container.Data;
                        existingContainer.SetHash(container.Hash);
                        _logger.Information("Updated existing content container in DropBearFile.");
                    }
                }
            }

            Result<Stream> streamResult;
            // Convert to stream and write the updated file or data
            if (existingFile == null)
            {
                streamResult = await ConvertToStreamAsync(data).ConfigureAwait(false);
            }
            else
            {
                streamResult = await ConvertToStreamAsync(existingFile).ConfigureAwait(false);
            }


            if (!streamResult.IsSuccess)
            {
                _logger.Warning("Failed to convert {DataType} to stream for update. Error: {ErrorMessage}",
                    typeof(T).Name, streamResult.ErrorMessage);
                return Result.Failure(streamResult.ErrorMessage);
            }

            var stream = streamResult.Value;
            await using (stream.ConfigureAwait(false))
            {
                var updateResult = await _storageManager.UpdateAsync(identifier, stream).ConfigureAwait(false);
                if (updateResult.IsSuccess)
                {
                    _logger.Information("Successfully updated {DataType} in file {FilePath}", typeof(T).Name,
                        identifier);
                    return Result.Success();
                }

                _logger.Warning("Failed to update file {FilePath}. Error: {ErrorMessage}", identifier,
                    updateResult.ErrorMessage);
                return updateResult;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error updating {DataType} in file {FilePath}", typeof(T).Name, identifier);
            return Result.Failure($"Error updating file: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Deletes the specified file.
    /// </summary>
    /// <param name="identifier">The path of the file or blob name for blob storage.</param>
    /// <param name="deleteFile">Whether to delete the entire file or just the content container.</param>
    /// <param name="containerToDelete">The content container to delete.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> DeleteFileAsync(string identifier, bool deleteFile = true,
        ContentContainer? containerToDelete = null)
    {
        _logger.Debug("Attempting to delete file {FilePath} or content container", identifier);

        try
        {
            // Validate the file path
            var validationResult = await ValidateFilePathAsync(identifier).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            identifier = GetFullPath(identifier);

            if (deleteFile || containerToDelete == null)
            {
                // Delete the entire file
                var deleteResult = await _storageManager.DeleteAsync(identifier).ConfigureAwait(false);
                if (deleteResult.IsSuccess)
                {
                    _logger.Information("Successfully deleted file {FilePath}", identifier);
                    return Result.Success();
                }

                _logger.Warning("Failed to delete file {FilePath}. Error: {ErrorMessage}", identifier,
                    deleteResult.ErrorMessage);
                return deleteResult;
            }

            // Read the existing file
            var readResult = await ReadFromFileAsync<DropBearFile>(identifier).ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                return Result.Failure(readResult.ErrorMessage);
            }

            var dropBearFile = readResult.Value;

            // Check if we are trying to remove the last content container
            if (dropBearFile.ContentContainers.Count == 1 && containerToDelete != null)
            {
                throw new InvalidOperationException("Cannot delete the last content container in the file.");
            }

            // Remove the specific content container
            var existingContainer = dropBearFile.ContentContainers.FirstOrDefault(c => c.Equals(containerToDelete));
            if (existingContainer != null)
            {
                dropBearFile.ContentContainers.Remove(existingContainer);
                _logger.Information("Deleted content container from file {FilePath}", identifier);

                // Write the updated file
                return await UpdateFileAsync(dropBearFile, identifier, true).ConfigureAwait(false);
            }

            return Result.Failure("Content container not found.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.Error(ex, "Error deleting file {FilePath}", identifier);
            return Result.Failure(ex.Message, ex);
        }
    }

    private string GetFullPath(string path)
    {
        if (Path.IsPathRooted(path) && path.StartsWith(_baseDirectory, StringComparison.Ordinal))
        {
            return path; // It's already a full path within the base directory
        }

        return Path.GetFullPath(Path.Combine(_baseDirectory, path));
    }

    private async Task<Result> ValidateFilePathAsync(string path)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(GetFullPath(path));
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
            _logger.Error(ex, "Error validating file path: {FilePath}", path);
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

    #region Content Container Helpers

    /// <summary>
    ///     Retrieves a specific <see cref="ContentContainer" /> by its content type.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to search for the content container.</param>
    /// <param name="contentType">The content type of the container to find.</param>
    /// <returns>The found <see cref="ContentContainer" />, or null if not found.</returns>
    public ContentContainer? GetContainerByContentType(DropBearFile file, string contentType)
    {
        try
        {
            return file.ContentContainers.FirstOrDefault(c =>
                c.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting content container by content type: {ContentType}", contentType);
            return null;
        }
    }

    /// <summary>
    ///     Retrieves a specific <see cref="ContentContainer" /> by its computed hash.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to search for the content container.</param>
    /// <param name="hash">The hash of the container to find.</param>
    /// <returns>The found <see cref="ContentContainer" />, or null if not found.</returns>
    public ContentContainer? GetContainerByHash(DropBearFile file, string hash)
    {
        try
        {
            return file.ContentContainers.FirstOrDefault(c => string.Equals(c.Hash, hash, StringComparison.Ordinal));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting content container by hash: {Hash}", hash);
            return null;
        }
    }

    /// <summary>
    ///     Lists all content types in the given <see cref="DropBearFile" />.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to list the content types from.</param>
    /// <returns>A list of content types present in the file.</returns>
    public IList<string> ListContainerTypes(DropBearFile file)
    {
        try
        {
            return file.ContentContainers.Select(c => c.ContentType).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error listing content container types.");
            return new List<string>();
        }
    }

    /// <summary>
    ///     Adds or updates a <see cref="ContentContainer" /> in the <see cref="DropBearFile" />.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to update.</param>
    /// <param name="newContainer">The new content container to add or update.</param>
    /// <returns>A result indicating success or failure of the operation.</returns>
    public Result AddOrUpdateContainer(DropBearFile file, ContentContainer newContainer)
    {
        try
        {
            var existingContainer = file.ContentContainers.FirstOrDefault(c => c.Equals(newContainer));

            if (existingContainer != null)
            {
                // Update existing container
                existingContainer.Data = newContainer.Data;
                existingContainer.SetHash(newContainer.Hash);
                _logger.Information("Updated existing content container.");
                return Result.Success();
            }

            // Add new container
            file.ContentContainers.Add(newContainer);
            _logger.Information("Added new content container.");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error adding or updating content container.");
            return Result.Failure("Error adding or updating container.");
        }
    }

    /// <summary>
    ///     Removes a specific <see cref="ContentContainer" /> by its content type.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> from which to remove the container.</param>
    /// <param name="contentType">The content type of the container to remove.</param>
    /// <returns>A result indicating success or failure of the operation.</returns>
    public Result RemoveContainerByContentType(DropBearFile file, string contentType)
    {
        try
        {
            var containerToRemove = GetContainerByContentType(file, contentType);
            if (containerToRemove == null)
            {
                return Result.Failure("Content container not found.");
            }

            if (file.ContentContainers.Count == 1)
            {
                return Result.Failure("Cannot remove the last content container.");
            }

            file.ContentContainers.Remove(containerToRemove);
            _logger.Information("Removed content container with content type: {ContentType}", contentType);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error removing content container by content type: {ContentType}", contentType);
            return Result.Failure("Error removing content container.");
        }
    }

    /// <summary>
    ///     Counts the total number of content containers in the given <see cref="DropBearFile" />.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to count content containers from.</param>
    /// <returns>The number of content containers.</returns>
    public int CountContainers(DropBearFile file)
    {
        try
        {
            return file.ContentContainers.Count;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error counting content containers.");
            return 0;
        }
    }

    /// <summary>
    ///     Counts the number of content containers of a specific type in the <see cref="DropBearFile" />.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to count from.</param>
    /// <param name="contentType">The content type to count.</param>
    /// <returns>The number of content containers of the specified type.</returns>
    public int CountContainersByType(DropBearFile file, string contentType)
    {
        try
        {
            return file.ContentContainers.Count(c =>
                c.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error counting content containers by content type: {ContentType}", contentType);
            return 0;
        }
    }

    /// <summary>
    ///     Replaces an existing <see cref="ContentContainer" /> with a new one.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to modify.</param>
    /// <param name="oldContainer">The container to replace.</param>
    /// <param name="newContainer">The new container to replace it with.</param>
    /// <returns>A result indicating success or failure of the operation.</returns>
    public Result ReplaceContainer(DropBearFile file, ContentContainer oldContainer, ContentContainer newContainer)
    {
        try
        {
            var existingContainer = file.ContentContainers.FirstOrDefault(c => c.Equals(oldContainer));
            if (existingContainer == null)
            {
                return Result.Failure("Content container not found.");
            }

            // Remove the old container and add the new one
            file.ContentContainers.Remove(existingContainer);
            file.ContentContainers.Add(newContainer);

            _logger.Information("Replaced content container in DropBearFile.");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error replacing content container.");
            return Result.Failure("Error replacing content container.");
        }
    }

    #endregion
}
