namespace DropBear.Codex.Hashing.Errors;

/// <summary>
///     Represents errors that occur during hash computation operations.
/// </summary>
public sealed record HashComputationError : HashingError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="HashComputationError" />.
    /// </summary>
    /// <param name="message">The error message describing the failure condition.</param>
    public HashComputationError(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Error indicating that input is empty or null.
    /// </summary>
    public static HashComputationError EmptyInput =>
        new("Input cannot be null or empty.");

    /// <summary>
    ///     Creates an error indicating that input is invalid for a specific reason.
    /// </summary>
    /// <param name="reason">The reason why the input is invalid.</param>
    /// <returns>A <see cref="HashComputationError" /> with a descriptive message.</returns>
    public static HashComputationError InvalidInput(string reason)
    {
        return new HashComputationError($"Invalid input: {reason}");
    }

    /// <summary>
    ///     Creates an error indicating that the hashing algorithm encountered an error.
    /// </summary>
    /// <param name="message">A message describing what went wrong.</param>
    /// <returns>A <see cref="HashComputationError" /> with a descriptive message.</returns>
    public static HashComputationError AlgorithmError(string message)
    {
        return new HashComputationError($"Error during hash computation: {message}");
    }
}
