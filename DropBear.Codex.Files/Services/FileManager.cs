#region

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
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
///     Supports reading, writing, updating, and deleting files (or blobs) via an <see cref="IStorageManager" />.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FileManager
{
    private readonly string _baseDirectory;
    private readonly ILogger _logger;
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
    /// <exception cref="PlatformNotSupportedException">Thrown when not running on Windows.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="storageManager" /> is null.</exception>
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
    ///     Writes data to the specified file (or blob) path asynchronously.
    /// </summary>
    /// <typeparam name="T">Type of data to write (e.g., <see cref="DropBearFile" /> or <c>byte[]</c>).</typeparam>
    /// <param name="data">The data to write.</param>
    /// <param name="identifier">A file path or blob name (depending on <see cref="IStorageManager" />).</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public async Task<Result> WriteToFileAsync<T>(T data, string identifier)
    {
        _logger.Debug("Attempting to write {DataType} to file {FilePath}", typeof(T).Name, identifier);

        try
        {
            // Validate path & directory permissions
            var validationResult = await ValidateFilePathAsync(identifier).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            identifier = GetFullPath(identifier);

            // Convert the data to a stream
            var streamResult = await ConvertToStreamAsync(data).ConfigureAwait(false);
            if (!streamResult.IsSuccess)
            {
                _logger.Warning("Failed to convert {DataType} to stream. Error: {ErrorMessage}",
                    typeof(T).Name, streamResult.ErrorMessage);
                return Result.Failure(streamResult.ErrorMessage);
            }

            // Write the stream via the storage manager
            var stream = streamResult.Value;
            await using (stream.ConfigureAwait(false))
            {
                var writeResult = await _storageManager.WriteAsync(identifier, stream).ConfigureAwait(false);
                if (writeResult.IsSuccess)
                {
                    _logger.Information("Successfully wrote {DataType} to file {FilePath}",
                        typeof(T).Name, identifier);
                    return Result.Success();
                }

                _logger.Warning("Failed to write to file {FilePath}. Error: {ErrorMessage}",
                    identifier, writeResult.ErrorMessage);
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
    ///     Reads data of type <typeparamref name="T" /> from the specified file (or blob) path asynchronously.
    /// </summary>
    /// <typeparam name="T">Type of data to read (e.g., <see cref="DropBearFile" /> or <c>byte[]</c>).</typeparam>
    /// <param name="identifier">A file path or blob name.</param>
    /// <returns>A <see cref="Result{T}" /> with the read data or an error.</returns>
    public async Task<Result<T>> ReadFromFileAsync<T>(string identifier)
    {
        _logger.Debug("Attempting to read {DataType} from file {FilePath}", typeof(T).Name, identifier);

        try
        {
            // Validate path & directory permissions
            var validationResult = await ValidateFilePathAsync(identifier).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return Result<T>.Failure(validationResult.ErrorMessage);
            }

            identifier = GetFullPath(identifier);

            // Read the file via the storage manager
            var streamResult = await _storageManager.ReadAsync(identifier).ConfigureAwait(false);
            if (!streamResult.IsSuccess)
            {
                _logger.Warning("Failed to read from file {FilePath}. Error: {ErrorMessage}",
                    identifier, streamResult.ErrorMessage);
                return Result<T>.Failure(streamResult.ErrorMessage);
            }

            var stream = streamResult.Value;
            await using (stream.ConfigureAwait(false))
            {
                // Convert the stream back into T
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
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public async Task<Result> UpdateFileAsync<T>(T data, string identifier, bool overwrite = false)
    {
        _logger.Debug("Attempting to update {DataType} in file {FilePath}", typeof(T).Name, identifier);

        try
        {
            // Validate path & directory permissions
            var validationResult = await ValidateFilePathAsync(identifier).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            identifier = GetFullPath(identifier);

            // If not overwriting and type is DropBearFile, attempt a partial update
            DropBearFile? existingFile = null;
            if (!overwrite && typeof(T) == typeof(DropBearFile))
            {
                // Read the existing file
                var readResult = await ReadFromFileAsync<DropBearFile>(identifier).ConfigureAwait(false);
                if (readResult.IsSuccess)
                {
                    existingFile = readResult.Value;
                }
                else
                {
                    _logger.Warning("No existing file found at {FilePath}. Error: {ErrorMessage}",
                        identifier, readResult.ErrorMessage);
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
                        _logger.Information("Added new content container to DropBearFile.");
                    }
                    else if (container.Data == null)
                    {
                        // Remove this container if data is null
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
                        // Update existing container
                        existingContainer.Data = container.Data;
                        existingContainer.SetHash(container.Hash);
                        _logger.Information("Updated existing content container in DropBearFile.");
                    }
                }
            }

            // Convert updated object to stream
            Result<Stream> streamResult;
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
                // Use storage manager to update the file
                var updateResult = await _storageManager.UpdateAsync(identifier, stream).ConfigureAwait(false);
                if (updateResult.IsSuccess)
                {
                    _logger.Information("Successfully updated {DataType} in file {FilePath}",
                        typeof(T).Name, identifier);
                    return Result.Success();
                }

                _logger.Warning("Failed to update file {FilePath}. Error: {ErrorMessage}",
                    identifier, updateResult.ErrorMessage);
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
    ///     Deletes an entire file (or blob) or a single <see cref="ContentContainer" /> from the file.
    /// </summary>
    /// <param name="identifier">A file path or blob name.</param>
    /// <param name="deleteFile">If <c>true</c>, deletes the entire file.</param>
    /// <param name="containerToDelete">
    ///     If specified, deletes only that container instead of the entire file (unless <paramref name="deleteFile" /> is
    ///     <c>true</c>).
    /// </param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public async Task<Result> DeleteFileAsync(
        string identifier,
        bool deleteFile = true,
        ContentContainer? containerToDelete = null)
    {
        _logger.Debug("Attempting to delete file {FilePath} or specific content container", identifier);

        try
        {
            // Validate path & directory permissions
            var validationResult = await ValidateFilePathAsync(identifier).ConfigureAwait(false);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            identifier = GetFullPath(identifier);

            // If deleting the entire file, just call storage manager
            if (deleteFile || containerToDelete == null)
            {
                var deleteResult = await _storageManager.DeleteAsync(identifier).ConfigureAwait(false);
                if (deleteResult.IsSuccess)
                {
                    _logger.Information("Successfully deleted file {FilePath}", identifier);
                    return Result.Success();
                }

                _logger.Warning("Failed to delete file {FilePath}. Error: {ErrorMessage}",
                    identifier, deleteResult.ErrorMessage);
                return deleteResult;
            }

            // If containerToDelete is specified, remove only that container from the file
            var readResult = await ReadFromFileAsync<DropBearFile>(identifier).ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                return Result.Failure(readResult.ErrorMessage);
            }

            var dropBearFile = readResult.Value;

            // Ensure we do not remove the last container
            if (dropBearFile.ContentContainers.Count == 1 && containerToDelete != null)
            {
                throw new InvalidOperationException("Cannot delete the last content container in the file.");
            }

            var existingContainer = dropBearFile.ContentContainers
                .FirstOrDefault(c => c.Equals(containerToDelete));

            if (existingContainer != null)
            {
                dropBearFile.ContentContainers.Remove(existingContainer);
                _logger.Information("Deleted content container from file {FilePath}", identifier);

                // Update the file with the new container list
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

    /// <summary>
    ///     Retrieves a specific <see cref="ContentContainer" /> from a <see cref="DropBearFile" /> by content type.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to search.</param>
    /// <param name="contentType">The content type to match.</param>
    /// <returns>The matching <see cref="ContentContainer" /> or <c>null</c> if not found.</returns>
    public ContentContainer? GetContainerByContentType(DropBearFile file, string contentType)
    {
        try
        {
            return file.ContentContainers
                .FirstOrDefault(c => c.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting content container by content type: {ContentType}", contentType);
            return null;
        }
    }

    /// <summary>
    ///     Retrieves a specific <see cref="ContentContainer" /> from a <see cref="DropBearFile" /> by hash.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to search.</param>
    /// <param name="hash">The hash to match.</param>
    /// <returns>The matching <see cref="ContentContainer" /> or <c>null</c> if not found.</returns>
    public ContentContainer? GetContainerByHash(DropBearFile file, string hash)
    {
        try
        {
            return file.ContentContainers
                .FirstOrDefault(c => string.Equals(c.Hash, hash, StringComparison.Ordinal));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting content container by hash: {Hash}", hash);
            return null;
        }
    }

    /// <summary>
    ///     Lists all distinct content types in the <see cref="DropBearFile" />.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> whose containers are listed.</param>
    /// <returns>A list of content types present in <paramref name="file" />.</returns>
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
    ///     Adds or updates a single <see cref="ContentContainer" /> in the <see cref="DropBearFile" />.
    ///     If it exists, updates data; otherwise adds a new container.
    /// </summary>
    /// <param name="file">The target <see cref="DropBearFile" />.</param>
    /// <param name="newContainer">The <see cref="ContentContainer" /> to add or update.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public Result AddOrUpdateContainer(DropBearFile file, ContentContainer newContainer)
    {
        try
        {
            var existingContainer = file.ContentContainers.FirstOrDefault(c => c.Equals(newContainer));
            if (existingContainer != null)
            {
                existingContainer.Data = newContainer.Data;
                existingContainer.SetHash(newContainer.Hash);
                _logger.Information("Updated existing content container.");
                return Result.Success();
            }

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
    ///     Removes a <see cref="ContentContainer" /> from the <see cref="DropBearFile" /> by content type,
    ///     ensuring not to remove the last container.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to modify.</param>
    /// <param name="contentType">The content type of the container to remove.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
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
    ///     Counts the total number of <see cref="ContentContainer" /> objects in the <see cref="DropBearFile" />.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to count containers in.</param>
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
    ///     Counts the number of <see cref="ContentContainer" /> objects in the <see cref="DropBearFile" />
    ///     that match a specific content type.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to search.</param>
    /// <param name="contentType">The content type to match.</param>
    /// <returns>The count of matching containers.</returns>
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
    ///     Replaces an old <see cref="ContentContainer" /> in the <see cref="DropBearFile" /> with a new one.
    /// </summary>
    /// <param name="file">The <see cref="DropBearFile" /> to modify.</param>
    /// <param name="oldContainer">The old container to remove.</param>
    /// <param name="newContainer">The new container to add in its place.</param>
    /// <returns>A <see cref="Result" /> indicating success or failure.</returns>
    public Result ReplaceContainer(DropBearFile file, ContentContainer oldContainer, ContentContainer newContainer)
    {
        try
        {
            var existingContainer = file.ContentContainers.FirstOrDefault(c => c.Equals(oldContainer));
            if (existingContainer == null)
            {
                return Result.Failure("Content container not found.");
            }

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

    #region Private/Helper Methods

    // Builds full path within base directory
    private string GetFullPath(string path)
    {
        if (Path.IsPathRooted(path) && path.StartsWith(_baseDirectory, StringComparison.Ordinal))
        {
            // Already a full path
            return path;
        }

        return Path.GetFullPath(Path.Combine(_baseDirectory, path));
    }

    // Validates directory existence and permissions
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

            return await HasWritePermissionOnDirAsync(directoryPath).ConfigureAwait(false)
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

    private static async Task AddDirectorySecurityAsync(
        string path,
        string account,
        FileSystemRights rights,
        AccessControlType controlType)
    {
        try
        {
            var dInfo = new DirectoryInfo(path);
            var dSecurity = await Task.Run(() => dInfo.GetAccessControl()).ConfigureAwait(false);

            dSecurity.AddAccessRule(new FileSystemAccessRule(
                account,
                rights,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
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
            // If it's a DropBearFile, serialize asynchronously to a stream
            DropBearFile file => Result<Stream>.Success(await file.ToStreamAsync(_logger).ConfigureAwait(false)),

            // If it's a byte array, just wrap in a MemoryStream
            byte[] byteArray => Result<Stream>.Success(new MemoryStream(byteArray)),

            // Add other supported types here
            _ => Result<Stream>.Failure($"Unsupported type for write operation: {typeof(T).Name}")
        };
    }

    private async Task<Result<T>> ConvertFromStreamAsync<T>(Stream stream)
    {
        if (typeof(T) == typeof(byte[]))
        {
            // Copy stream to a byte array
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return Result<T>.Success((T)(object)ms.ToArray());
        }

        if (typeof(T) == typeof(DropBearFile))
        {
            // Use extension method to deserialize
            var file = await DropBearFileExtensions.FromStreamAsync(stream, _logger).ConfigureAwait(false);
            return Result<T>.Success((T)(object)file);
        }

        // Add other supported types here
        return Result<T>.Failure($"Unsupported type for read operation: {typeof(T).Name}");
    }

    #endregion
}
