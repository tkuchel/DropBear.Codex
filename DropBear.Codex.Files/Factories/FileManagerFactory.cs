#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Services;
using DropBear.Codex.Files.StorageManagers;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Factories;

/// <summary>
///     Factory class for creating various types of FileManager instances.
/// </summary>
[SupportedOSPlatform("windows")]
public static class FileManagerFactory
{
    private static readonly ILogger Logger = Log.ForContext(typeof(FileManagerFactory));

    /// <summary>
    ///     Creates a FileManager instance configured for local storage.
    /// </summary>
    /// <param name="baseDirectory">The base directory for local storage.</param>
    /// <returns>A FileManager configured for local storage.</returns>
    public static FileManager CreateLocalFileManager(string baseDirectory = @"C:\Data")
    {
        Logger.Debug("Creating LocalFileManager with base directory: {BaseDirectory}", baseDirectory);

        try
        {
            var logger = LoggerFactory.Logger.ForContext<FileManager>();
            var memoryStreamManager = new RecyclableMemoryStreamManager();
            var localStorageManager = new LocalStorageManager(memoryStreamManager, logger, baseDirectory);

            var fileManager = new FileManager(localStorageManager);
            Logger.Information("Successfully created LocalFileManager");
            return fileManager;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating LocalFileManager");
            throw;
        }
    }

    /// <summary>
    ///     Creates a FileManager instance configured for blob storage.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name.</param>
    /// <param name="accountKey">The shared key for the Azure Storage account.</param>
    /// <param name="containerName">The default container name for blob storage.</param>
    /// <returns>A FileManager configured for blob storage.</returns>
    public static FileManager CreateBlobFileManager(string accountName, string accountKey, string containerName)
    {
        Logger.Debug("Creating BlobFileManager for account: {AccountName}", accountName);

        try
        {
            var logger = LoggerFactory.Logger.ForContext<FileManager>();
            var memoryStreamManager = new RecyclableMemoryStreamManager();
            var blobStorage = BlobStorageFactory.CreateAzureBlobStorage(accountName, accountKey);
            var blobStorageManager = new BlobStorageManager(blobStorage, memoryStreamManager, logger, containerName);

            var fileManager = new FileManager(blobStorageManager);
            Logger.Information("Successfully created BlobFileManager");
            return fileManager;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating BlobFileManager");
            throw;
        }
    }

    /// <summary>
    ///     Creates a FileManager instance configured for blob storage asynchronously.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name.</param>
    /// <param name="accountKey">The shared key for the Azure Storage account.</param>
    /// <param name="containerName">The default container name for blob storage.</param>
    /// <returns>A Task representing the asynchronous operation, containing a FileManager configured for blob storage.</returns>
    public static async Task<FileManager> CreateBlobFileManagerAsync(string accountName, string accountKey,
        string containerName)
    {
        Logger.Debug("Creating BlobFileManager asynchronously for account: {AccountName}", accountName);

        try
        {
            var logger = LoggerFactory.Logger.ForContext<FileManager>();
            var memoryStreamManager = new RecyclableMemoryStreamManager();
            var blobStorage = await BlobStorageFactory.CreateAzureBlobStorageAsync(accountName, accountKey)
                .ConfigureAwait(false);
            var blobStorageManager = new BlobStorageManager(blobStorage, memoryStreamManager, logger, containerName);

            var fileManager = new FileManager(blobStorageManager);
            Logger.Information("Successfully created BlobFileManager asynchronously");
            return fileManager;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating BlobFileManager asynchronously");
            throw;
        }
    }

    /// <summary>
    ///     Creates a No-Op FileManager instance, which does nothing.
    /// </summary>
    /// <returns>A FileManager configured for No-Op.</returns>
    public static FileManager CreateNoOpFileManager()
    {
        Logger.Debug("Creating NoOpFileManager");

        try
        {
            var fileManager = new FileManager(null);
            Logger.Information("Successfully created NoOpFileManager");
            return fileManager;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating NoOpFileManager");
            throw;
        }
    }
}
