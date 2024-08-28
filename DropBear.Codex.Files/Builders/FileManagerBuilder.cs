﻿#region

using System.Runtime.Versioning;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Enums;
using DropBear.Codex.Files.Factories;
using DropBear.Codex.Files.Interfaces;
using DropBear.Codex.Files.Services;
using DropBear.Codex.Files.StorageManagers;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Builders;

/// <summary>
///     Builder class for creating instances of <see cref="FileManager" /> with various storage strategies.
/// </summary>
[SupportedOSPlatform("windows")]
public class FileManagerBuilder
{
    private readonly ILogger _logger;
    private BlobStorageManager? _blobStorageManager;
    private LocalStorageManager? _localStorageManager;
    private StorageStrategy _strategy = StorageStrategy.NoOperation;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FileManagerBuilder" /> class with logging support.
    /// </summary>
    public FileManagerBuilder()
    {
        _logger = LoggerFactory.Logger.ForContext<FileManagerBuilder>();
    }

    /// <summary>
    ///     Configures the FileManager to use local storage.
    /// </summary>
    /// <param name="baseDirectory">The base directory for local storage.</param>
    /// <returns>The current <see cref="FileManagerBuilder" /> instance.</returns>
    public FileManagerBuilder UseLocalStorage(string baseDirectory = @"C:\Data")
    {
        var memoryStreamManager = new RecyclableMemoryStreamManager();
        _localStorageManager = new LocalStorageManager(memoryStreamManager, _logger, baseDirectory);
        _strategy = StorageStrategy.LocalOnly;
        _logger.Information("Configured FileManager to use local storage with base directory: {BaseDirectory}",
            baseDirectory);
        return this;
    }

    /// <summary>
    ///     Configures the FileManager to use Azure blob storage.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name.</param>
    /// <param name="accountKey">The shared key for the Azure Storage account.</param>
    /// <param name="containerName">The default container name for blob storage.</param>
    /// <returns>The current <see cref="FileManagerBuilder" /> instance.</returns>
    public FileManagerBuilder UseBlobStorage(string accountName, string accountKey, string containerName)
    {
        var memoryStreamManager = new RecyclableMemoryStreamManager();
        var blobStorage = BlobStorageFactory.CreateAzureBlobStorage(accountName, accountKey);
        _blobStorageManager = new BlobStorageManager(blobStorage, memoryStreamManager, _logger, containerName);
        _strategy = StorageStrategy.BlobOnly;
        _logger.Information(
            "Configured FileManager to use Azure Blob Storage with account: {AccountName}, container: {ContainerName}",
            accountName, containerName);
        return this;
    }

    /// <summary>
    ///     Configures the FileManager to perform no operations.
    /// </summary>
    /// <returns>The current <see cref="FileManagerBuilder" /> instance.</returns>
    public FileManagerBuilder WithNoOperation()
    {
        _strategy = StorageStrategy.NoOperation;
        _logger.Information("Configured FileManager to perform no operations.");
        return this;
    }

    /// <summary>
    ///     Builds and returns the configured <see cref="FileManager" /> instance.
    /// </summary>
    /// <returns>A configured <see cref="FileManager" /> instance.</returns>
    public FileManager Build()
    {
        _logger.Information("Building FileManager with strategy: {Strategy}", _strategy);

        IStorageManager storageManager = _strategy switch
        {
            StorageStrategy.LocalOnly => _localStorageManager ??
                                         throw new InvalidOperationException(
                                             "Local storage manager is not configured."),
            StorageStrategy.BlobOnly => _blobStorageManager ??
                                        throw new InvalidOperationException("Blob storage manager is not configured."),
            StorageStrategy.NoOperation => throw new InvalidOperationException(
                "No operation strategy does not require a storage manager."),
            _ => throw new InvalidOperationException("Unsupported storage strategy.")
        };

        return new FileManager(_strategy, storageManager);
    }
}