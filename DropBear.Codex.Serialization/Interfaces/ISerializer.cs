namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for serializers.
/// </summary>
public interface ISerializer
{
    /// <summary>
    ///     Asynchronously serializes the provided value.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///     A task representing the asynchronous operation, which upon completion returns the serialized data as a byte
    ///     array.
    /// </returns>
    Task<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously deserializes the provided byte array into an instance of type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize into.</typeparam>
    /// <param name="data">The byte array containing the serialized data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///     A task representing the asynchronous operation, which upon completion returns the deserialized value of type
    ///     <typeparamref name="T" />.
    /// </returns>
    Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default);
}
