#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Files.Errors;
using FluentStorage;
using FluentStorage.Blobs;
using Microsoft.Extensions.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Factories;

/// <summary>
///     Factory class for creating various types of blob storage instances using FluentStorage.
///     Supports synchronous and asynchronous creation of Azure Blob Storage instances.
/// </summary>
public static partial class BlobStorageFactory
{
    private static readonly Microsoft.Extensions.Logging.ILogger Logger = CreateLogger();

    /// <summary>
    ///     Creates an <see cref="IBlobStorage" /> instance for Azure Blob Storage using shared key authentication.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name.</param>
    /// <param name="accountKey">The shared key for the Azure Storage account.</param>
    /// <returns>
    ///     A <see cref="Result{T, TError}" /> containing either the <see cref="IBlobStorage" /> instance
    ///     or a <see cref="StorageError" /> if creation failed.
    /// </returns>
    public static Result<IBlobStorage, StorageError> CreateAzureBlobStorage(string accountName, string accountKey)
    {
        LogCreatingAzureBlobStorage(Logger, accountName);

        var validationResult = ValidateInput(accountName, nameof(accountName));
        if (!validationResult.IsSuccess)
        {
            return Result<IBlobStorage, StorageError>.Failure(validationResult.Error!);
        }

        validationResult = ValidateInput(accountKey, nameof(accountKey));
        if (!validationResult.IsSuccess)
        {
            return Result<IBlobStorage, StorageError>.Failure(validationResult.Error!);
        }

        try
        {
            var storage = StorageFactory.Blobs.AzureBlobStorageWithSharedKey(accountName, accountKey);
            if (storage == null)
            {
                return Result<IBlobStorage, StorageError>.Failure(
                    StorageError.CreationFailed("Storage factory returned null"));
            }

            LogAzureBlobStorageCreated(Logger, accountName);
            return Result<IBlobStorage, StorageError>.Success(storage);
        }
        catch (Exception ex)
        {
            LogAzureBlobStorageCreationError(Logger, ex, accountName);
            return Result<IBlobStorage, StorageError>.Failure(
                StorageError.CreationFailed(ex.Message),
                ex);
        }
    }

    /// <summary>
    ///     Asynchronously creates an <see cref="IBlobStorage" /> instance for Azure Blob Storage using shared key
    ///     authentication.
    /// </summary>
    /// <param name="accountName">The Azure Storage account name.</param>
    /// <param name="accountKey">The shared key for the Azure Storage account.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains either
    ///     the <see cref="IBlobStorage" /> instance or a <see cref="StorageError" /> if creation failed.
    /// </returns>
    public static async Task<Result<IBlobStorage, StorageError>> CreateAzureBlobStorageAsync(
        string accountName,
        string accountKey,
        CancellationToken cancellationToken = default)
    {
        LogCreatingAzureBlobStorageAsync(Logger, accountName);

        var validationResult = ValidateInput(accountName, nameof(accountName));
        if (!validationResult.IsSuccess)
        {
            return Result<IBlobStorage, StorageError>.Failure(validationResult.Error!);
        }

        validationResult = ValidateInput(accountKey, nameof(accountKey));
        if (!validationResult.IsSuccess)
        {
            return Result<IBlobStorage, StorageError>.Failure(validationResult.Error!);
        }

        try
        {
            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();

            var storage = await Task.Run(() =>
                    StorageFactory.Blobs.AzureBlobStorageWithSharedKey(accountName, accountKey),
                cancellationToken).ConfigureAwait(false);

            if (storage == null)
            {
                return Result<IBlobStorage, StorageError>.Failure(
                    StorageError.CreationFailed("Storage factory returned null"));
            }

            LogAzureBlobStorageCreatedAsync(Logger, accountName);
            return Result<IBlobStorage, StorageError>.Success(storage);
        }
        catch (OperationCanceledException ex)
        {
            LogAzureBlobStorageCreationCanceled(Logger, accountName);
            return Result<IBlobStorage, StorageError>.Failure(
                StorageError.CreationFailed("Operation was canceled"),
                ex);
        }
        catch (Exception ex)
        {
            LogAzureBlobStorageCreationErrorAsync(Logger, ex, accountName);
            return Result<IBlobStorage, StorageError>.Failure(
                StorageError.CreationFailed(ex.Message),
                ex);
        }
    }

    /// <summary>
    ///     Validates that the provided <paramref name="input" /> is neither null nor empty.
    /// </summary>
    /// <param name="input">The input string to validate.</param>
    /// <param name="paramName">The name of the parameter.</param>
    /// <returns>
    ///     A <see cref="Result{T, TError}" /> indicating success if the input is valid,
    ///     or a <see cref="StorageError" /> if validation failed.
    /// </returns>
    private static Result<Unit, StorageError> ValidateInput(string input, string paramName)
    {
        if (string.IsNullOrEmpty(input))
        {
            return Result<Unit, StorageError>.Failure(
                StorageError.InvalidInput(paramName, "cannot be null or empty"));
        }

        return Result<Unit, StorageError>.Success(Unit.Value);
    }

    private static Microsoft.Extensions.Logging.ILogger CreateLogger()
    {
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Log.ForContext(typeof(BlobStorageFactory)));
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
        });
        return loggerFactory.CreateLogger(nameof(BlobStorageFactory));
    }

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Creating Azure Blob Storage with account name: {AccountName}")]
    static partial void LogCreatingAzureBlobStorage(Microsoft.Extensions.Logging.ILogger logger, string accountName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "Successfully created Azure Blob Storage for account: {AccountName}")]
    static partial void LogAzureBlobStorageCreated(Microsoft.Extensions.Logging.ILogger logger, string accountName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Error creating Azure Blob Storage for account: {AccountName}")]
    static partial void LogAzureBlobStorageCreationError(Microsoft.Extensions.Logging.ILogger logger, Exception ex, string accountName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Creating Azure Blob Storage asynchronously with account name: {AccountName}")]
    static partial void LogCreatingAzureBlobStorageAsync(Microsoft.Extensions.Logging.ILogger logger, string accountName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "Successfully created Azure Blob Storage asynchronously for account: {AccountName}")]
    static partial void LogAzureBlobStorageCreatedAsync(Microsoft.Extensions.Logging.ILogger logger, string accountName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "Azure Blob Storage creation was canceled for account: {AccountName}")]
    static partial void LogAzureBlobStorageCreationCanceled(Microsoft.Extensions.Logging.ILogger logger, string accountName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "Error creating Azure Blob Storage asynchronously for account: {AccountName}")]
    static partial void LogAzureBlobStorageCreationErrorAsync(Microsoft.Extensions.Logging.ILogger logger, Exception ex, string accountName);

    #endregion
}
