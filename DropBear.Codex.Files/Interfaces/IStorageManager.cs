#region

using DropBear.Codex.Core;

#endregion

namespace DropBear.Codex.Files.Interfaces;

/// <summary>
///     Defines the contract for a storage manager that handles basic storage operations.
/// </summary>
public interface IStorageManager
{
    /// <summary>
    ///     Writes data to storage asynchronously.
    /// </summary>
    /// <param name="identifier">The identifier of the data.</param>
    /// <param name="dataStream">The stream containing the data to write.</param>
    /// <param name="subDirectory">Optional subdirectory within the storage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a Result indicating success or failure.</returns>
    Task<Result> WriteAsync(string identifier, Stream dataStream, string? subDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads data from storage asynchronously.
    /// </summary>
    /// <param name="identifier">The identifier of the data to read.</param>
    /// <param name="subDirectory">Optional subdirectory within the storage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a Result with the data stream if successful.</returns>
    Task<Result<Stream>> ReadAsync(string identifier, string? subDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates existing data in storage asynchronously.
    /// </summary>
    /// <param name="identifier">The identifier of the data to update.</param>
    /// <param name="newDataStream">The stream containing the new data.</param>
    /// <param name="subDirectory">Optional subdirectory within the storage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a Result indicating success or failure.</returns>
    Task<Result> UpdateAsync(string identifier, Stream newDataStream, string? subDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes data from storage asynchronously.
    /// </summary>
    /// <param name="identifier">The identifier of the data to delete.</param>
    /// <param name="subDirectory">Optional subdirectory within the storage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing a Result indicating success or failure.</returns>
    Task<Result> DeleteAsync(string identifier, string? subDirectory = null,
        CancellationToken cancellationToken = default);
}
