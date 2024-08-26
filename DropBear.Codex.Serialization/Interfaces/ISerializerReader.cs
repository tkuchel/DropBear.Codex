namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for serializer readers.
/// </summary>
public interface ISerializerReader
{
    /// <summary>
    ///     Asynchronously deserializes the data from the provided stream.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="stream">The stream containing the serialized data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the deserialized data.</returns>
    Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);
}
