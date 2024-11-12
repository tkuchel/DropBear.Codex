namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Base record for all Result error types
/// </summary>
public abstract record ResultError
{
    private const string DefaultErrorMessage = "An unknown error occurred";

    /// <summary>
    ///     Initializes a new instance of ResultError
    /// </summary>
    /// <param name="message">The error message</param>
    /// <exception cref="ArgumentException">If message is null or whitespace</exception>
    protected ResultError(string message)
    {
        Message = ValidateAndFormatMessage(message);
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    ///     Gets the error message
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    ///     Gets the UTC timestamp when the error was created
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    ///     Gets whether this is a default/unknown error
    /// </summary>
    public bool IsDefaultError => Message.Equals(DefaultErrorMessage, StringComparison.Ordinal);

    /// <summary>
    ///     Gets how long ago this error occurred
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - Timestamp;

    /// <summary>
    ///     Creates a string representation of the error
    /// </summary>
    public override string ToString()
    {
        return $"{GetType().Name}: {Message} (occurred {FormatAge(Age)} ago)";
    }

    /// <summary>
    ///     Validates and formats the error message
    /// </summary>
    private static string ValidateAndFormatMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return DefaultErrorMessage;
        }

        // Normalize line endings and remove extra whitespace
        return message
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();
    }

    /// <summary>
    ///     Formats a TimeSpan into a human-readable age string
    /// </summary>
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

    /// <summary>
    ///     Creates a new error with an updated message
    /// </summary>
    protected ResultError WithUpdatedMessage(string newMessage)
    {
        return this with { Message = ValidateAndFormatMessage(newMessage) };
    }
}
