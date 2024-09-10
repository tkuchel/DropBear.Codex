#region

using FluentStorage;
using FluentStorage.Blobs;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Factories;

/// <summary>
///     Factory class for creating various types of blob storage instances.
/// </summary>
public static class BlobStorageFactory
{
    private static readonly ILogger Logger = Log.ForContext(typeof(BlobStorageFactory));

    /// <summary>
    ///     Creates an IBlobStorage instance for Azure Blob Storage using shared key authentication.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name.</param>
    /// <param name="accountKey">The shared key for the Azure Storage account.</param>
    /// <returns>An instance of IBlobStorage configured for Azure Blob Storage.</returns>
    /// <exception cref="ArgumentException">Thrown when accountName or accountKey is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the creation of Azure Blob Storage fails.</exception>
    public static IBlobStorage CreateAzureBlobStorage(string accountName, string accountKey)
    {
        Logger.Debug("Creating Azure Blob Storage with account name: {AccountName}", accountName);

        ValidateInput(accountName, nameof(accountName));
        ValidateInput(accountKey, nameof(accountKey));

        try
        {
            var storage = StorageFactory.Blobs.AzureBlobStorageWithSharedKey(accountName, accountKey);
            if (storage == null)
            {
                throw new InvalidOperationException("Failed to create Azure Blob Storage.");
            }

            Logger.Information("Successfully created Azure Blob Storage for account: {AccountName}", accountName);
            return storage;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating Azure Blob Storage for account: {AccountName}", accountName);
            throw;
        }
    }

    /// <summary>
    ///     Creates an IBlobStorage instance for Azure Blob Storage using shared key authentication asynchronously.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name.</param>
    /// <param name="accountKey">The shared key for the Azure Storage account.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an instance of IBlobStorage
    ///     configured for Azure Blob Storage.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when accountName or accountKey is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the creation of Azure Blob Storage fails.</exception>
    public static async Task<IBlobStorage> CreateAzureBlobStorageAsync(string accountName, string accountKey)
    {
        Logger.Debug("Creating Azure Blob Storage asynchronously with account name: {AccountName}", accountName);

        ValidateInput(accountName, nameof(accountName));
        ValidateInput(accountKey, nameof(accountKey));

        try
        {
            var storage = await Task.Run(() =>
                StorageFactory.Blobs.AzureBlobStorageWithSharedKey(accountName, accountKey)).ConfigureAwait(false);
            if (storage == null)
            {
                throw new InvalidOperationException("Failed to create Azure Blob Storage.");
            }

            Logger.Information("Successfully created Azure Blob Storage asynchronously for account: {AccountName}",
                accountName);
            return storage;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating Azure Blob Storage asynchronously for account: {AccountName}",
                accountName);
            throw;
        }
    }

    private static void ValidateInput(string input, string paramName)
    {
        if (string.IsNullOrEmpty(input))
        {
            throw new ArgumentException($"{paramName} cannot be null or empty.", paramName);
        }
    }
}
