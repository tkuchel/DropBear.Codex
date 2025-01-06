#region

using FluentStorage;
using FluentStorage.Blobs;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Factories;

/// <summary>
///     Factory class for creating various types of blob storage instances using FluentStorage.
///     Supports synchronous and asynchronous creation of Azure Blob Storage instances.
/// </summary>
public static class BlobStorageFactory
{
    private static readonly ILogger Logger = Log.ForContext(typeof(BlobStorageFactory));

    /// <summary>
    ///     Creates an <see cref="IBlobStorage" /> instance for Azure Blob Storage using shared key authentication.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name.</param>
    /// <param name="accountKey">The shared key for the Azure Storage account.</param>
    /// <returns>An <see cref="IBlobStorage" /> configured for Azure Blob Storage.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="accountName" /> or <paramref name="accountKey" /> is null or empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if creation of the Azure Blob Storage instance fails.
    /// </exception>
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
    ///     Asynchronously creates an <see cref="IBlobStorage" /> instance for Azure Blob Storage using shared key
    ///     authentication.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name.</param>
    /// <param name="accountKey">The shared key for the Azure Storage account.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result is an <see cref="IBlobStorage" />
    ///     instance configured for Azure Blob Storage.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="accountName" /> or <paramref name="accountKey" /> is null or empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if creation of the Azure Blob Storage instance fails.
    /// </exception>
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

    /// <summary>
    ///     Validates that the provided <paramref name="input" /> is neither null nor empty.
    /// </summary>
    /// <param name="input">The input string to validate.</param>
    /// <param name="paramName">The name of the parameter (for exception messages).</param>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="input" /> is null or empty.
    /// </exception>
    private static void ValidateInput(string input, string paramName)
    {
        if (string.IsNullOrEmpty(input))
        {
            throw new ArgumentException($"{paramName} cannot be null or empty.", paramName);
        }
    }
}
