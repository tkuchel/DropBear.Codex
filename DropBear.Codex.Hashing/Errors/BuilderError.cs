namespace DropBear.Codex.Hashing.Errors;

/// <summary>
///     Represents errors that occur during builder operations.
/// </summary>
public sealed record BuilderError : HashingError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="BuilderError" />.
    /// </summary>
    /// <param name="message">The error message describing the failure condition.</param>
    /// <param name="timestamp">Optional custom timestamp for the error. Defaults to UTC now.</param>
    public BuilderError(string message, DateTime? timestamp = null)
        : base(message, timestamp)
    {
    }

    /// <summary>
    ///     Error indicating that a requested hasher is not registered.
    /// </summary>
    /// <param name="key">The key that was not found.</param>
    /// <returns>A <see cref="BuilderError" /> with a descriptive message.</returns>
    public static BuilderError HasherNotFound(string key)
    {
        return new BuilderError($"No hashing service registered for key: {key}");
    }
}
