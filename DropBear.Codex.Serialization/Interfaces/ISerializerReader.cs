#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;

#endregion

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
    /// <returns>A result containing either the deserialized data or an error.</returns>
    Task<Result<T, SerializationError>> DeserializeAsync<T>(Stream stream,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the options being used by this reader.
    /// </summary>
    /// <returns>A dictionary containing the reader options.</returns>
    IDictionary<string, object> GetReaderOptions();
}
