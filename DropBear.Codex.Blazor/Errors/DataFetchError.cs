#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Base error type for data retrieval and manipulation operations in MochaDBV4.
///     Provides a foundation for more specific error types related to data operations.
/// </summary>
public record DataFetchError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DataFetchError" /> class
    ///     with a specific error message.
    /// </summary>
    /// <param name="message">The error message describing the data operation failure.</param>
    public DataFetchError(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataFetchError" /> class
    ///     with a specific error message and operation name.
    /// </summary>
    /// <param name="message">The error message describing the data operation failure.</param>
    /// <param name="operationName">The name of the operation that failed.</param>
    public DataFetchError(string message, string operationName) : base(message)
    {
        OperationName = operationName;
        WithMetadata("Operation", operationName);
    }

    /// <summary>
    ///     Gets the name of the operation that failed, if specified.
    /// </summary>
    public string? OperationName { get; }

    /// <summary>
    ///     Gets or sets the type of error that occurred.
    /// </summary>
    public ErrorType ErrorType { get; init; } = ErrorType.General;

    /// <summary>
    ///     Creates a DataFetchError for a timeout situation.
    /// </summary>
    /// <param name="operationName">The name of the operation that timed out.</param>
    /// <param name="timeoutSeconds">The timeout duration in seconds.</param>
    /// <returns>A configured DataFetchError for the timeout.</returns>
    public static DataFetchError Timeout(string operationName, int timeoutSeconds)
    {
        return new DataFetchError(
            $"The operation timed out after {timeoutSeconds} seconds",
            operationName) { ErrorType = ErrorType.Timeout }.WithMetadata("TimeoutSeconds", timeoutSeconds);
    }

    /// <summary>
    ///     Creates a DataFetchError for a not found situation.
    /// </summary>
    /// <param name="entityType">The type of entity that wasn't found.</param>
    /// <param name="identifier">The identifier that was used in the search.</param>
    /// <returns>A configured DataFetchError for the not found condition.</returns>
    public static DataFetchError NotFound(string entityType, string identifier)
    {
        return new DataFetchError(
                $"{entityType} with identifier '{identifier}' was not found",
                $"Find{entityType}") { ErrorType = ErrorType.NotFound }
            .WithMetadata("EntityType", entityType)
            .WithMetadata("Identifier", identifier);
    }

    /// <summary>
    ///     Creates a DataFetchError for a permission issue.
    /// </summary>
    /// <param name="operationName">The operation that the user doesn't have permission for.</param>
    /// <returns>A configured DataFetchError for the permission denied condition.</returns>
    public static DataFetchError PermissionDenied(string operationName)
    {
        return new DataFetchError(
            "You do not have permission to perform this operation",
            operationName) { ErrorType = ErrorType.PermissionDenied };
    }

    /// <summary>
    ///     Adds metadata to the error context.
    ///     Overrides the base implementation to return the correct derived type.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>A new instance with updated metadata.</returns>
    public new DataFetchError WithMetadata(string key, object value)
    {
        // Call the base implementation which returns ResultError
        var baseResult = base.WithMetadata(key, value);

        // Create a new DataFetchError with the same properties
        return this with
        {
            // Copy the updated metadata dictionary from the base result
            Metadata = baseResult.Metadata
        };
    }
}
