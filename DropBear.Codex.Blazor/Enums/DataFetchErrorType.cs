namespace DropBear.Codex.Blazor.Enums;

/// <summary>
///     Defines the types of data fetch errors for better categorization and handling.
/// </summary>
public enum DataFetchErrorType
{
    /// <summary>
    ///     Unknown error type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///     Operation timed out.
    /// </summary>
    Timeout = 1,

    /// <summary>
    ///     Network connectivity error.
    /// </summary>
    NetworkError = 2,

    /// <summary>
    ///     Authentication or authorization failure.
    /// </summary>
    AuthenticationError = 3,

    /// <summary>
    ///     Requested resource not found.
    /// </summary>
    NotFound = 4,

    /// <summary>
    ///     Validation error in request data.
    /// </summary>
    ValidationError = 5,

    /// <summary>
    ///     Server returned an error.
    /// </summary>
    ServerError = 6,

    /// <summary>
    ///     Service temporarily unavailable.
    /// </summary>
    TemporaryServiceUnavailable = 7,

    /// <summary>
    ///     Rate limit exceeded.
    /// </summary>
    RateLimited = 8,

    /// <summary>
    ///     Data parsing or deserialization error.
    /// </summary>
    DataParsingError = 9,

    /// <summary>
    ///     Operation was cancelled.
    /// </summary>
    OperationCancelled = 10
}
