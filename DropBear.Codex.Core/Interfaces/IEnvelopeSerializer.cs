#region

using DropBear.Codex.Core.Envelopes;

#endregion

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Defines methods for serializing and deserializing envelopes across different formats.
///     Supports both string-based (e.g., JSON) and binary (e.g., MessagePack) serialization strategies.
/// </summary>
public interface IEnvelopeSerializer
{
    /// <summary>
    ///     Serializes the specified envelope to a string representation.
    ///     <para>
    ///         Useful for human-readable or text-based storage and transmission.
    ///         Ensure that the envelope is not null to avoid exceptions.
    ///     </para>
    /// </summary>
    /// <typeparam name="T">The type of the envelope payload.</typeparam>
    /// <param name="envelope">The envelope to serialize.</param>
    /// <returns>A string representing the serialized envelope.</returns>
    string Serialize<T>(Envelope<T> envelope);

    /// <summary>
    ///     Deserializes a string into an envelope.
    ///     <para>
    ///         Throws exceptions if the data is null/whitespace or if the underlying
    ///         serialization engine fails to parse the string.
    ///     </para>
    /// </summary>
    /// <typeparam name="T">The type of the envelope payload.</typeparam>
    /// <param name="data">The serialized envelope data.</param>
    /// <returns>An envelope instance.</returns>
    Envelope<T> Deserialize<T>(string data);

    /// <summary>
    ///     Serializes the envelope to a compact binary format.
    ///     <para>
    ///         Optimized for storage efficiency and performance, particularly useful
    ///         for network transmission or compact storage.
    ///     </para>
    /// </summary>
    /// <typeparam name="T">The type of the envelope payload.</typeparam>
    /// <param name="envelope">The envelope to serialize.</param>
    /// <returns>A byte array representing the serialized envelope.</returns>
    byte[] SerializeToBinary<T>(Envelope<T> envelope);

    /// <summary>
    ///     Deserializes a binary representation into an envelope.
    ///     <para>
    ///         Throws exceptions if the data is null/empty or if the underlying
    ///         serializer fails to parse the binary representation.
    ///     </para>
    /// </summary>
    /// <typeparam name="T">The type of the envelope payload.</typeparam>
    /// <param name="data">The serialized binary envelope data.</param>
    /// <returns>An envelope instance.</returns>
    Envelope<T> DeserializeFromBinary<T>(byte[] data);
}
