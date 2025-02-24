#region

using DropBear.Codex.Core.Envelopes.Serializers;

#endregion

namespace DropBear.Codex.Core.Envelopes.Extensions;

public static class EnvelopeSerializationExtensions
{
    /// <summary>
    ///     Serializes the envelope to a string using the specified or default serializer.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="envelope">The envelope to serialize.</param>
    /// <param name="serializer">Optional serializer. Defaults to JSON if not provided.</param>
    /// <returns>A serialized string representation of the envelope.</returns>
    public static string ToSerializedString<T>(
        this Envelope<T> envelope,
        IEnvelopeSerializer? serializer = null)
    {
        serializer ??= new JsonEnvelopeSerializer();
        return serializer.Serialize(envelope);
    }

    /// <summary>
    ///     Serializes the envelope to a binary format using the specified or default serializer.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="envelope">The envelope to serialize.</param>
    /// <param name="serializer">Optional serializer. Defaults to MessagePack if not provided.</param>
    /// <returns>A byte array representing the serialized envelope.</returns>
    public static byte[] ToSerializedBinary<T>(
        this Envelope<T> envelope,
        IEnvelopeSerializer? serializer = null)
    {
        serializer ??= new MessagePackEnvelopeSerializer();
        return serializer.SerializeToBinary(envelope);
    }

    /// <summary>
    ///     Deserializes an envelope from a string representation.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="data">The serialized envelope data.</param>
    /// <param name="serializer">Optional serializer. Defaults to JSON if not provided.</param>
    /// <returns>A deserialized envelope instance.</returns>
    public static Envelope<T> FromSerializedString<T>(
        string data,
        IEnvelopeSerializer? serializer = null)
    {
        serializer ??= new JsonEnvelopeSerializer();
        return serializer.Deserialize<T>(data);
    }

    /// <summary>
    ///     Deserializes an envelope from a binary representation.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="data">The serialized binary envelope data.</param>
    /// <param name="serializer">Optional serializer. Defaults to MessagePack if not provided.</param>
    /// <returns>A deserialized envelope instance.</returns>
    public static Envelope<T> FromSerializedBinary<T>(
        byte[] data,
        IEnvelopeSerializer? serializer = null)
    {
        serializer ??= new MessagePackEnvelopeSerializer();
        return serializer.DeserializeFromBinary<T>(data);
    }
}
