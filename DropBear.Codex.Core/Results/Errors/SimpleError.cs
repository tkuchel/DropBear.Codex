using DropBear.Codex.Core.Results.Base;

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     A simple error with just a message.
///     Use this for basic error scenarios.
/// </summary>
public sealed record SimpleError : ResultError
{
    /// <summary>
    ///     Initializes a new SimpleError.
    /// </summary>
    public SimpleError(string message) : base(message)
    {
        Message = message;
    }

    /// <summary>
    ///     Creates a new SimpleError with the specified message.
    /// </summary>
    public static SimpleError Create(string message) => new(message);

    /// <summary>
    ///     Creates a new SimpleError from an exception.
    /// </summary>
    public static SimpleError FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var error = new SimpleError(exception.Message);
        var withMetadata = error
            .WithMetadata("ExceptionType", exception.GetType().Name);

        if (!string.IsNullOrEmpty(exception.StackTrace))
        {
            withMetadata = withMetadata.WithMetadata("StackTrace", exception.StackTrace);
        }

        return withMetadata as SimpleError ?? error;
    }
}
