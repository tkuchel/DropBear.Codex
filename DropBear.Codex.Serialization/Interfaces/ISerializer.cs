#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;

#endregion

namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for serializers, defining methods to serialize and deserialize data.
/// </summary>
public interface ISerializer
{
    /// <summary>
    ///     Asynchronously serializes the provided value to a byte array.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A result containing either the serialized data as a byte array or a serialization error.</returns>
    Task<Result<byte[], SerializationError>> SerializeAsync<T>(T value, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously deserializes the provided byte array into an instance of type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="data">The byte array containing the serialized data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A result containing either the deserialized value or a serialization error.</returns>
    Task<Result<T, SerializationError>> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the serializer's capabilities and configuration.
    /// </summary>
    /// <returns>A dictionary of capabilities and their values.</returns>
    IReadOnlyDictionary<string, object> GetCapabilities();
}
