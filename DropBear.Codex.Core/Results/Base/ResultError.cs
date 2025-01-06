namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     An abstract record representing an error in a result-based operation.
///     Derived types can provide domain-specific error information.
/// </summary>
public abstract record ResultError
{
    private const string DefaultErrorMessage = "An unknown error occurred";

    /// <summary>
    ///     Initializes a new instance of the <see cref="ResultError" /> record.
    /// </summary>
    /// <param name="message">The error message describing the failure condition.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="message" /> is null or whitespace.</exception>
    protected ResultError(string message)
    {
        Message = ValidateAndFormatMessage(message);
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    ///     Gets the error message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    ///     Gets the UTC timestamp when this error was created.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    ///     Indicates whether this error is a default/unknown error (i.e., no message provided).
    /// </summary>
    public bool IsDefaultError => Message.Equals(DefaultErrorMessage, StringComparison.Ordinal);

    /// <summary>
    ///     Gets how long ago (relative to now) this error occurred.
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - Timestamp;

    /// <summary>
    ///     Creates a string representation of this error, including how long ago it occurred.
    /// </summary>
    public override string ToString()
    {
        return $"{GetType().Name}: {Message} (occurred {FormatAge(Age)} ago)";
    }

    /// <summary>
    ///     Creates a new error object with an updated message.
    /// </summary>
    /// <param name="newMessage">The new error message.</param>
    /// <returns>A new <see cref="ResultError" /> instance with the updated message.</returns>
    protected ResultError WithUpdatedMessage(string newMessage)
    {
        return this with { Message = ValidateAndFormatMessage(newMessage) };
    }

    private static string ValidateAndFormatMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return DefaultErrorMessage;
        }

        // Normalize line endings and trim whitespace
        return message
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalMilliseconds < 1000)
        {
            return $"{age.TotalMilliseconds:F0}ms";
        }

        if (age.TotalSeconds < 60)
        {
            return $"{age.TotalSeconds:F1}s";
        }

        if (age.TotalMinutes < 60)
        {
            return $"{age.TotalMinutes:F1}m";
        }

        if (age.TotalHours < 24)
        {
            return $"{age.TotalHours:F1}h";
        }

        return $"{age.TotalDays:F1}d";
    }
}
