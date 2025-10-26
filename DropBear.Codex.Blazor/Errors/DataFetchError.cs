#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Extensions;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents an error that occurred during data fetch operations.
///     Enhanced for .NET 9 with improved error context and performance.
/// </summary>
public sealed record DataFetchError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DataFetchError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DataFetchError(string message) : base(message) { }

    /// <summary>
    ///     Gets or sets the operation type for categorization.
    /// </summary>
    public DataFetchErrorType ErrorType { get; init; } = DataFetchErrorType.Unknown;

    /// <summary>
    ///     Gets or sets the name of the operation that failed.
    /// </summary>
    public string? OperationName { get; init; }

    /// <summary>
    ///     Gets whether this error is retryable.
    /// </summary>
    public bool IsRetryable => ErrorType switch
    {
        DataFetchErrorType.Timeout => true,
        DataFetchErrorType.NetworkError => true,
        DataFetchErrorType.TemporaryServiceUnavailable => true,
        DataFetchErrorType.RateLimited => true,
        _ => false
    };

    /// <summary>
    ///     Creates an error for a fetch timeout.
    /// </summary>
    /// <param name="operationName">The name of the operation that timed out.</param>
    /// <param name="timeoutSeconds">The timeout in seconds.</param>
    /// <returns>A new <see cref="DataFetchError"/> instance.</returns>
    public static DataFetchError Timeout(string operationName, double timeoutSeconds)
    {
        return new DataFetchError($"Data fetch operation '{operationName}' timed out after {timeoutSeconds:F1}s")
        {
            ErrorType = DataFetchErrorType.Timeout,
            OperationName = operationName
        };
    }

    /// <summary>
    ///     Creates an error for a network failure.
    /// </summary>
    /// <param name="operationName">The name of the operation that failed.</param>
    /// <param name="details">Details about the network error.</param>
    /// <returns>A new <see cref="DataFetchError"/> instance.</returns>
    public static DataFetchError NetworkError(string operationName, string details)
    {
        return new DataFetchError($"Network error during '{operationName}': {details}")
        {
            ErrorType = DataFetchErrorType.NetworkError,
            OperationName = operationName
        };
    }

    /// <summary>
    ///     Creates an error for a not found scenario.
    /// </summary>
    /// <param name="resourceName">The name of the resource that was not found.</param>
    /// <returns>A new <see cref="DataFetchError"/> instance.</returns>
    public static DataFetchError NotFound(string resourceName)
    {
        return new DataFetchError($"Resource '{resourceName}' not found")
        {
            ErrorType = DataFetchErrorType.NotFound,
            OperationName = resourceName
        };
    }

    /// <summary>
    ///     Creates an error for an unauthorized access scenario.
    /// </summary>
    /// <param name="operationName">The name of the operation that was unauthorized.</param>
    /// <returns>A new <see cref="DataFetchError"/> instance.</returns>
    public static DataFetchError Unauthorized(string operationName)
    {
        return new DataFetchError($"Unauthorized access to '{operationName}'")
        {
            ErrorType = DataFetchErrorType.AuthenticationError,
            OperationName = operationName
        };
    }

    /// <summary>
    ///     Creates an error for a server error scenario.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="details">Details about the server error.</param>
    /// <returns>A new <see cref="DataFetchError"/> instance.</returns>
    public static DataFetchError ServerError(string operationName, string details)
    {
        return new DataFetchError($"Server error during '{operationName}': {details}")
        {
            ErrorType = DataFetchErrorType.ServerError,
            OperationName = operationName
        };
    }

    /// <summary>
    ///     Creates an error for a general fetch failure.
    /// </summary>
    /// <param name="operationName">The name of the operation that failed.</param>
    /// <param name="details">Details about the failure.</param>
    /// <returns>A new <see cref="DataFetchError"/> instance.</returns>
    public static DataFetchError FetchFailed(string operationName, string details)
    {
        return new DataFetchError($"Data fetch '{operationName}' failed: {details}")
        {
            OperationName = operationName
        };
    }
}
