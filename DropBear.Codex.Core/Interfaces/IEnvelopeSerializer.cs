using DropBear.Codex.Core.Envelopes;

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
/// Defines methods for serializing and deserializing envelopes.
/// </summary>
public interface IEnvelopeSerializer
{
    /// <summary>
    /// Serializes the specified envelope to a string.
    /// </summary>
    /// <typeparam name="T">The type of payload.</typeparam>
    /// <param name="envelope">The envelope to serialize.</param>
    /// <returns>A string representing the serialized envelope.</returns>
    string Serialize<T>(Envelope<T> envelope);

    /// <summary>
    /// Deserializes a string into an envelope.
    /// </summary>
    /// <typeparam name="T">The type of payload.</typeparam>
    /// <param name="data">The serialized envelope data.</param>
    /// <returns>An envelope instance.</returns>
    Envelope<T> Deserialize<T>(string data);
}
