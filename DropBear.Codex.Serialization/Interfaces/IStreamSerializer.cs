namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Defines methods for stream-based serialization and deserialization.
///     This interface can handle direct stream inputs and outputs for serialization,
///     which is ideal for large data processing or when data should not be fully loaded into memory.
/// </summary>
public interface IStreamSerializer
{
    /// <summary>
    ///     Serializes an object to a stream asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="stream">The stream to serialize the object to.</param>
    /// <param name="value">The object to serialize.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the serialization process.</param>
    /// <returns>A task that represents the asynchronous serialization operation.</returns>
    Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deserializes an object from a stream asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize.</typeparam>
    /// <param name="stream">The stream to deserialize the object from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the deserialization process.</param>
    /// <returns>A task that represents the asynchronous deserialization operation and yields the deserialized object.</returns>
    Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);
}
