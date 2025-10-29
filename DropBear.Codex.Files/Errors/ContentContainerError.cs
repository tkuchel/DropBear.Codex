namespace DropBear.Codex.Files.Errors;

/// <summary>
///     Represents errors that occur during content container operations.
/// </summary>
public sealed record ContentContainerError : FilesError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="ContentContainerError" />.
    /// </summary>
    /// <param name="message">The error message describing the failure condition.</param>
    public ContentContainerError(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Error indicating that no data is available or data is invalid.
    /// </summary>
    public static ContentContainerError InvalidData =>
        new("No data available or data is invalid");

    /// <summary>
    ///     Error indicating that serialization operation failed.
    /// </summary>
    public static ContentContainerError SerializationFailed =>
        new("Serialization operation failed");

    /// <summary>
    ///     Error indicating that deserialization operation failed.
    /// </summary>
    public static ContentContainerError DeserializationFailed =>
        new("Deserialization operation failed");

    /// <summary>
    ///     Error indicating that data integrity check failed.
    /// </summary>
    public static ContentContainerError HashVerificationFailed =>
        new("Data integrity check failed");

    /// <summary>
    ///     Creates an error indicating that a specific provider was not found.
    /// </summary>
    /// <param name="providerName">The name of the provider that was not found.</param>
    /// <returns>A <see cref="ContentContainerError" /> with a descriptive message.</returns>
    public static ContentContainerError ProviderNotFound(string providerName)
    {
        return new ContentContainerError($"Provider for {providerName} not found");
    }
}
