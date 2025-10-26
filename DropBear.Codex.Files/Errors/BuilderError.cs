namespace DropBear.Codex.Files.Errors;

/// <summary>
///     Represents errors that occur during builder operations.
/// </summary>
public sealed record BuilderError : FilesError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="BuilderError"/>.
    /// </summary>
    /// <param name="message">The error message describing the failure condition.</param>
    public BuilderError(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Creates an error indicating that a build operation failed.
    /// </summary>
    /// <param name="message">A message describing why the build failed.</param>
    /// <returns>A <see cref="BuilderError"/> with a descriptive message.</returns>
    public static BuilderError BuildFailed(string message) =>
        new($"Build operation failed: {message}");

    /// <summary>
    ///     Creates an error indicating that a build operation failed due to an exception.
    /// </summary>
    /// <param name="ex">The exception that caused the build to fail.</param>
    /// <returns>A <see cref="BuilderError"/> with a descriptive message.</returns>
    public static BuilderError BuildFailed(Exception ex) =>
        new($"Build operation failed: {ex.Message}");
}
