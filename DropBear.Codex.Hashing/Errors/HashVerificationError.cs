namespace DropBear.Codex.Hashing.Errors;

/// <summary>
///     Represents errors that occur during hash verification operations.
/// </summary>
public sealed record HashVerificationError : HashingError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="HashVerificationError" />.
    /// </summary>
    /// <param name="message">The error message describing the failure condition.</param>
    public HashVerificationError(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Error indicating that inputs for verification are missing.
    /// </summary>
    public static HashVerificationError MissingInput =>
        new("Input and expected hash cannot be null or empty.");

    /// <summary>
    ///     Error indicating that the hash format is invalid.
    /// </summary>
    public static HashVerificationError InvalidFormat =>
        new("Expected hash format is invalid.");

    /// <summary>
    ///     Error indicating that salt is required but not provided.
    /// </summary>
    public static HashVerificationError MissingSalt =>
        new("Salt is required for verification.");

    /// <summary>
    ///     Error indicating that verification failed due to hash mismatch.
    /// </summary>
    public static HashVerificationError HashMismatch =>
        new("Verification failed - hash mismatch.");
}
