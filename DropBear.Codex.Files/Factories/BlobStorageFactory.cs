#region

using FluentStorage;
using FluentStorage.Blobs;

#endregion

namespace DropBear.Codex.Files.Factories;

public static class BlobStorageFactory
{
    /// <summary>
    ///     Creates an IBlobStorage instance for Azure Blob Storage using shared key authentication.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name.</param>
    /// <param name="accountKey">The shared key for the Azure Storage account.</param>
    /// <returns>An instance of IBlobStorage configured for Azure Blob Storage.</returns>
    public static IBlobStorage CreateAzureBlobStorage(string accountName, string accountKey)
    {
        if (string.IsNullOrEmpty(accountName))
        {
            throw new ArgumentException("Account name cannot be null or empty.", nameof(accountName));
        }

        if (string.IsNullOrEmpty(accountKey))
        {
            throw new ArgumentException("Account key cannot be null or empty.", nameof(accountKey));
        }

        // Create the Azure Blob Storage with shared key
        var storage = StorageFactory.Blobs.AzureBlobStorageWithSharedKey(accountName, accountKey);
        if (storage == null)
        {
            throw new InvalidOperationException("Failed to create Azure Blob Storage.");
        }

        return storage;
    }
}
