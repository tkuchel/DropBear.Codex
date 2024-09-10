#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Factories;
using DropBear.Codex.Files.Interfaces;
using DropBear.Codex.Files.Services;
using DropBear.Codex.Files.StorageManagers;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Builders;

/// <summary>
///     Builder class for creating instances of <see cref="FileManager" /> with various storage configurations.
/// </summary>
[SupportedOSPlatform("windows")]
public class FileManagerBuilder
{
    private readonly ILogger _logger;
    private string? _baseDirectory;
    private RecyclableMemoryStreamManager? _memoryStreamManager;
    private IStorageManager? _storageManager; // Single storage manager (Local or Blob)

    /// <summary>
    ///     Initializes a new instance of the <see cref="FileManagerBuilder" /> class.
    /// </summary>
    public FileManagerBuilder()
    {
        _logger = LoggerFactory.Logger.ForContext<FileManagerBuilder>();
    }

    /// <summary>
    ///     Configures the FileManager to use a custom <see cref="RecyclableMemoryStreamManager" />.
    /// </summary>
    /// <param name="memoryStreamManager">The custom memory stream manager.</param>
    /// <returns>The current <see cref="FileManagerBuilder" /> instance.</returns>
    public FileManagerBuilder WithMemoryStreamManager(RecyclableMemoryStreamManager memoryStreamManager)
    {
        if (memoryStreamManager == null)
        {
            throw new ArgumentNullException(nameof(memoryStreamManager), "MemoryStreamManager cannot be null.");
        }

        _memoryStreamManager = memoryStreamManager;
        _logger.Debug("Configured custom MemoryStreamManager.");
        return this;
    }

    /// <summary>
    ///     Configures the FileManager to use local storage.
    /// </summary>
    /// <param name="baseDirectory">The base directory for local storage.</param>
    /// <returns>The current <see cref="FileManagerBuilder" /> instance.</returns>
    public FileManagerBuilder UseLocalStorage(string baseDirectory = @"C:\Data")
    {
        if (string.IsNullOrEmpty(baseDirectory))
        {
            throw new ArgumentException("Base directory cannot be null or empty.", nameof(baseDirectory));
        }

        if (_memoryStreamManager == null)
        {
            _memoryStreamManager = new RecyclableMemoryStreamManager();
            _logger.Debug("Default MemoryStreamManager created for local storage.");
        }

        _baseDirectory = baseDirectory;
        _storageManager = new LocalStorageManager(_memoryStreamManager, _logger);
        _logger.Information("Configured FileManager to use local storage with base directory: {BaseDirectory}",
            baseDirectory);
        return this;
    }

    /// <summary>
    ///     Configures the FileManager to use Azure Blob Storage.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name.</param>
    /// <param name="accountKey">The shared key for the Azure Storage account.</param>
    /// <param name="containerName">The default container name for blob storage.</param>
    /// <returns>The current <see cref="FileManagerBuilder" /> instance.</returns>
    public FileManagerBuilder UseBlobStorage(string accountName, string accountKey, string containerName)
    {
        if (string.IsNullOrEmpty(accountName))
        {
            throw new ArgumentException("Account name cannot be null or empty.", nameof(accountName));
        }

        if (string.IsNullOrEmpty(accountKey))
        {
            throw new ArgumentException("Account key cannot be null or empty.", nameof(accountKey));
        }

        if (string.IsNullOrEmpty(containerName))
        {
            throw new ArgumentException("Container name cannot be null or empty.", nameof(containerName));
        }

        if (_memoryStreamManager == null)
        {
            _memoryStreamManager = new RecyclableMemoryStreamManager();
            _logger.Debug("Default MemoryStreamManager created for blob storage.");
        }

        var blobStorage = BlobStorageFactory.CreateAzureBlobStorage(accountName, accountKey);
        _storageManager = new BlobStorageManager(blobStorage, _memoryStreamManager, _logger, containerName);
        _logger.Information(
            "Configured FileManager to use Azure Blob Storage with account: {AccountName}, container: {ContainerName}",
            accountName, containerName);
        return this;
    }

    /// <summary>
    ///     Configures the FileManager to use Azure Blob Storage asynchronously.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name.</param>
    /// <param name="accountKey">The shared key for the Azure Storage account.</param>
    /// <param name="containerName">The default container name for blob storage.</param>
    /// <returns>
    ///     A Task representing the asynchronous operation, containing the current <see cref="FileManagerBuilder" />
    ///     instance.
    /// </returns>
    public async Task<FileManagerBuilder> UseBlobStorageAsync(string accountName, string accountKey,
        string containerName)
    {
        if (string.IsNullOrEmpty(accountName))
        {
            throw new ArgumentException("Account name cannot be null or empty.", nameof(accountName));
        }

        if (string.IsNullOrEmpty(accountKey))
        {
            throw new ArgumentException("Account key cannot be null or empty.", nameof(accountKey));
        }

        if (string.IsNullOrEmpty(containerName))
        {
            throw new ArgumentException("Container name cannot be null or empty.", nameof(containerName));
        }

        if (_memoryStreamManager == null)
        {
            _memoryStreamManager = new RecyclableMemoryStreamManager();
            _logger.Debug("Default MemoryStreamManager created for blob storage.");
        }

        var blobStorage = await BlobStorageFactory.CreateAzureBlobStorageAsync(accountName, accountKey)
            .ConfigureAwait(false);
        _storageManager = new BlobStorageManager(blobStorage, _memoryStreamManager, _logger, containerName);
        _logger.Information(
            "Configured FileManager to use Azure Blob Storage asynchronously with account: {AccountName}, container: {ContainerName}",
            accountName, containerName);
        return this;
    }

    /// <summary>
    ///     Builds and returns the configured <see cref="FileManager" /> instance.
    /// </summary>
    /// <returns>The built <see cref="FileManager" /> instance.</returns>
    public FileManager Build()
    {
        try
        {
            if (_storageManager == null)
            {
                throw new InvalidOperationException("A storage manager (local or blob) must be configured.");
            }

            _logger.Information("Building FileManager with configured storage manager.");
            return new FileManager(_baseDirectory ?? string.Empty, _storageManager);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error building FileManager");
            throw;
        }
    }
}
