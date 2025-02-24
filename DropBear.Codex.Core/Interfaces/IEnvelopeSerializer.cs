#region

using System.Text.Json;
using DropBear.Codex.Core.Envelopes;

#endregion

/// <summary>
///     Defines methods for serializing and deserializing envelopes across different formats.
///     Supports both string-based (e.g., JSON) and binary (e.g., MessagePack) serialization strategies.
/// </summary>
public interface IEnvelopeSerializer
{
    /// <summary>
    ///     Serializes the specified envelope to a string representation.
    /// </summary>
    /// <typeparam name="T">The type of payload.</typeparam>
    /// <param name="envelope">The envelope to serialize.</param>
    /// <returns>A string representing the serialized envelope.</returns>
    /// <remarks>
    ///     Useful for human-readable or text-based storage and transmission.
    /// </remarks>
    string Serialize<T>(Envelope<T> envelope);

    /// <summary>
    ///     Deserializes a string into an envelope.
    /// </summary>
    /// <typeparam name="T">The type of payload.</typeparam>
    /// <param name="data">The serialized envelope data.</param>
    /// <returns>An envelope instance.</returns>
    /// <exception cref="JsonException">Thrown when deserialization fails.</exception>
    Envelope<T> Deserialize<T>(string data);

    /// <summary>
    ///     Serializes the envelope to a compact binary format.
    /// </summary>
    /// <typeparam name="T">The type of payload.</typeparam>
    /// <param name="envelope">The envelope to serialize.</param>
    /// <returns>A byte array representing the serialized envelope.</returns>
    /// <remarks>
    ///     Optimized for storage efficiency and performance,
    ///     particularly useful for network transmission or compact storage.
    /// </remarks>
    byte[] SerializeToBinary<T>(Envelope<T> envelope);

    /// <summary>
    ///     Deserializes a binary representation into an envelope.
    /// </summary>
    /// <typeparam name="T">The type of payload.</typeparam>
    /// <param name="data">The serialized binary envelope data.</param>
    /// <returns>An envelope instance.</returns>
    /// <exception cref="MessagePackSerializationException">Thrown when binary deserialization fails.</exception>
    Envelope<T> DeserializeFromBinary<T>(byte[] data);
}
