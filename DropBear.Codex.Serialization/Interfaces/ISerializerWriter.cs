#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;

#endregion

namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for serializer writers.
/// </summary>
public interface ISerializerWriter
{
    /// <summary>
    ///     Asynchronously serializes the provided value to the specified stream.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="stream">The stream to write the serialized data to.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or an error.</returns>
    Task<Result<Unit, SerializationError>> SerializeAsync<T>(Stream stream, T value,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the options being used by this writer.
    /// </summary>
    /// <returns>A dictionary containing the writer options.</returns>
    IDictionary<string, object> GetWriterOptions();
}
