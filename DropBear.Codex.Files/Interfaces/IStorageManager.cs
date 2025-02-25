#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Files.Errors;

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
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A Result indicating success (Unit) or failure with error details.</returns>
    Task<Result<Unit, StorageError>> WriteAsync(
        string identifier,
        Stream dataStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads data from storage asynchronously.
    /// </summary>
    /// <param name="identifier">The identifier of the data to read.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A Result containing the data stream if successful, or error details if failed.</returns>
    Task<Result<Stream, StorageError>> ReadAsync(
        string identifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates existing data in storage asynchronously.
    /// </summary>
    /// <param name="identifier">The identifier of the data to update.</param>
    /// <param name="newDataStream">The stream containing the new data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A Result indicating success (Unit) or failure with error details.</returns>
    Task<Result<Unit, StorageError>> UpdateAsync(
        string identifier,
        Stream newDataStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes data from storage asynchronously.
    /// </summary>
    /// <param name="identifier">The identifier of the data to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A Result indicating success (Unit) or failure with error details.</returns>
    Task<Result<Unit, StorageError>> DeleteAsync(
        string identifier,
        CancellationToken cancellationToken = default);
}
