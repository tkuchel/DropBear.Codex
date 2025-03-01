#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents errors that occur during data grid operations.
///     Provides common error patterns and factory methods for consistent error handling.
/// </summary>
public sealed record DataGridError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DataGridError" /> class.
    /// </summary>
    /// <param name="message">The error message explaining the failure.</param>
    public DataGridError(string message) : base(message) { }

    /// <summary>
    ///     Creates an error for when data loading fails.
    /// </summary>
    /// <param name="details">Details about the loading failure.</param>
    /// <returns>A new <see cref="DataGridError" /> with appropriate message.</returns>
    public static DataGridError LoadingFailed(string details)
    {
        return new DataGridError($"Failed to load grid data: {details}");
    }

    /// <summary>
    ///     Creates an error for when search operations fail.
    /// </summary>
    /// <param name="searchTerm">The search term that caused the failure.</param>
    /// <param name="details">Details about the failure.</param>
    /// <returns>A new <see cref="DataGridError" /> with appropriate message.</returns>
    public static DataGridError SearchFailed(string searchTerm, string details)
    {
        return new DataGridError($"Failed to search for '{searchTerm}': {details}");
    }

    /// <summary>
    ///     Creates an error for when sorting operations fail.
    /// </summary>
    /// <param name="columnName">The name of the column being sorted.</param>
    /// <param name="details">Details about the failure.</param>
    /// <returns>A new <see cref="DataGridError" /> with appropriate message.</returns>
    public static DataGridError SortingFailed(string columnName, string details)
    {
        return new DataGridError($"Failed to sort by column '{columnName}': {details}");
    }

    /// <summary>
    ///     Creates an error for when export operations fail.
    /// </summary>
    /// <param name="details">Details about the failure.</param>
    /// <returns>A new <see cref="DataGridError" /> with appropriate message.</returns>
    public static DataGridError ExportFailed(string details)
    {
        return new DataGridError($"Failed to export data: {details}");
    }

    /// <summary>
    ///     Creates an error for when column configuration is invalid.
    /// </summary>
    /// <param name="columnName">The name of the problematic column.</param>
    /// <param name="details">Details about the configuration issue.</param>
    /// <returns>A new <see cref="DataGridError" /> with appropriate message.</returns>
    public static DataGridError InvalidColumnConfiguration(string columnName, string details)
    {
        return new DataGridError($"Invalid configuration for column '{columnName}': {details}");
    }

    /// <summary>
    ///     Creates an error for when pagination fails.
    /// </summary>
    /// <param name="details">Details about the failure.</param>
    /// <returns>A new <see cref="DataGridError" /> with appropriate message.</returns>
    public static DataGridError PaginationFailed(string details)
    {
        return new DataGridError($"Pagination operation failed: {details}");
    }
}
