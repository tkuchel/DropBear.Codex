#region

using DropBear.Codex.Core.Envelopes.Serializers;
using DropBear.Codex.Core.Interfaces;

#endregion

namespace DropBear.Codex.Core.Envelopes.Extensions;

/// <summary>
///     Provides extension methods for <see cref="Envelope{T}" /> that simplify
///     serialization to and from both string and binary formats.
/// </summary>
public static class EnvelopeSerializationExtensions
{
    // *** CHANGE 1: Store static singletons for default serializers
    private static readonly IEnvelopeSerializer DefaultJsonSerializer = new JsonEnvelopeSerializer();
    private static readonly IEnvelopeSerializer DefaultMessagePackSerializer = new MessagePackEnvelopeSerializer();

    /// <summary>
    ///     Serializes the given envelope to a string using the specified serializer,
    ///     or defaults to JSON if no serializer is provided.
    /// </summary>
    /// <typeparam name="T">The payload type of the envelope.</typeparam>
    /// <param name="envelope">The envelope to serialize.</param>
    /// <param name="serializer">An optional serializer. If <c>null</c>, defaults to <see cref="JsonEnvelopeSerializer" />.</param>
    /// <returns>A string representing the serialized envelope.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="envelope" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     This method delegates the actual serialization to the provided
    ///     or default serializer, which may itself throw exceptions (e.g. if the
    ///     envelope contents cannot be serialized).
    /// </remarks>
    public static string ToSerializedString<T>(
        this Envelope<T> envelope,
        IEnvelopeSerializer? serializer = null)
    {
        if (envelope == null)
        {
            throw new ArgumentNullException(nameof(envelope), "Envelope cannot be null.");
        }

        // *** CHANGE 2: Use static instance if serializer is null
        serializer ??= DefaultJsonSerializer;

        return serializer.Serialize(envelope);
    }

    /// <summary>
    ///     Serializes the given envelope to binary using the specified serializer,
    ///     or defaults to MessagePack if no serializer is provided.
    /// </summary>
    /// <typeparam name="T">The payload type of the envelope.</typeparam>
    /// <param name="envelope">The envelope to serialize.</param>
    /// <param name="serializer">
    ///     An optional serializer. If <c>null</c>, defaults to
    ///     <see cref="MessagePackEnvelopeSerializer" />.
    /// </param>
    /// <returns>A byte array representing the serialized envelope in binary form.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="envelope" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     This method delegates the actual serialization to the provided
    ///     or default serializer, which may itself throw exceptions.
    /// </remarks>
    public static byte[] ToSerializedBinary<T>(
        this Envelope<T> envelope,
        IEnvelopeSerializer? serializer = null)
    {
        if (envelope == null)
        {
            throw new ArgumentNullException(nameof(envelope), "Envelope cannot be null.");
        }

        // *** CHANGE 2: Use static instance if serializer is null
        serializer ??= DefaultMessagePackSerializer;

        return serializer.SerializeToBinary(envelope);
    }

    /// <summary>
    ///     Deserializes an envelope from the specified string using the given serializer,
    ///     or defaults to JSON if no serializer is provided.
    /// </summary>
    /// <typeparam name="T">The payload type of the envelope.</typeparam>
    /// <param name="data">The string that contains the serialized envelope.</param>
    /// <param name="serializer">An optional serializer. If <c>null</c>, defaults to <see cref="JsonEnvelopeSerializer" />.</param>
    /// <returns>A deserialized <see cref="Envelope{T}" /> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="data" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     The underlying serializer may throw exceptions if the data is invalid
    ///     or cannot be parsed into the expected envelope format.
    /// </remarks>
    public static Envelope<T> FromSerializedString<T>(
        string data,
        IEnvelopeSerializer? serializer = null)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "Serialized data cannot be null.");
        }

        // *** CHANGE 2: Use static instance if serializer is null
        serializer ??= DefaultJsonSerializer;

        return serializer.Deserialize<T>(data);
    }

    /// <summary>
    ///     Deserializes an envelope from the specified binary data using the given serializer,
    ///     or defaults to MessagePack if no serializer is provided.
    /// </summary>
    /// <typeparam name="T">The payload type of the envelope.</typeparam>
    /// <param name="data">The byte array that contains the serialized envelope in binary form.</param>
    /// <param name="serializer">
    ///     An optional serializer. If <c>null</c>, defaults to
    ///     <see cref="MessagePackEnvelopeSerializer" />.
    /// </param>
    /// <returns>A deserialized <see cref="Envelope{T}" /> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="data" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     The underlying serializer may throw exceptions if the data is invalid
    ///     or cannot be parsed into the expected envelope format.
    /// </remarks>
    public static Envelope<T> FromSerializedBinary<T>(
        byte[] data,
        IEnvelopeSerializer? serializer = null)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "Serialized data cannot be null.");
        }

        // *** CHANGE 2: Use static instance if serializer is null
        serializer ??= DefaultMessagePackSerializer;

        return serializer.DeserializeFromBinary<T>(data);
    }
}
