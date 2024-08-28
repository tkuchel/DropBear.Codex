#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Enums;
using DropBear.Codex.Files.Services;
using DropBear.Codex.Files.StorageManagers;
using Microsoft.IO;

#endregion

namespace DropBear.Codex.Files.Factories;

[SupportedOSPlatform("windows")]
public static class FileManagerFactory
{
    /// <summary>
    ///     Creates a FileManager instance configured for local storage.
    /// </summary>
    /// <param name="baseDirectory">The base directory for local storage.</param>
    /// <returns>A FileManager configured for local storage.</returns>
    public static FileManager CreateLocalFileManager(string baseDirectory = @"C:\Data")
    {
        var logger = LoggerFactory.Logger.ForContext<FileManager>();
        var memoryStreamManager = new RecyclableMemoryStreamManager();
        var localStorageManager = new LocalStorageManager(memoryStreamManager, logger, baseDirectory);

        return new FileManager(StorageStrategy.LocalOnly, localStorageManager);
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
        var logger = LoggerFactory.Logger.ForContext<FileManager>();
        var memoryStreamManager = new RecyclableMemoryStreamManager();
        var blobStorage = BlobStorageFactory.CreateAzureBlobStorage(accountName, accountKey);
        var blobStorageManager = new BlobStorageManager(blobStorage, memoryStreamManager, logger, containerName);

        return new FileManager(StorageStrategy.BlobOnly, blobStorageManager);
    }

    /// <summary>
    ///     Creates a No-Op FileManager instance, which does nothing.
    /// </summary>
    /// <returns>A FileManager configured for No-Op.</returns>
    public static FileManager CreateNoOpFileManager()
    {
        var logger = LoggerFactory.Logger.ForContext<FileManager>();
        return new FileManager(StorageStrategy.NoOperation, null);
    }
}
