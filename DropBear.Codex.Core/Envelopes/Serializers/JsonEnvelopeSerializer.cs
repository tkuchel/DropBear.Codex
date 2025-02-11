using System.Text.Json;
using DropBear.Codex.Core.Interfaces;

namespace DropBear.Codex.Core.Envelopes.Serializers;

/// <summary>
/// Default JSON-based envelope serializer.
/// </summary>
public sealed class JsonEnvelopeSerializer : IEnvelopeSerializer
{
    /// <inheritdoc />
    public string Serialize<T>(Envelope<T> envelope)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        // Call the public GetDto() method to retrieve the DTO for serialization.
        return JsonSerializer.Serialize(envelope.GetDto(), options);
    }

    /// <inheritdoc />
    public Envelope<T> Deserialize<T>(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
            throw new ArgumentException("Data cannot be null or empty.", nameof(data));

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        // Fully qualify the nested EnvelopeDto type.
        Envelope<T>.EnvelopeDto<T> dto = JsonSerializer.Deserialize<Envelope<T>.EnvelopeDto<T>>(data, options)
                                         ?? throw new InvalidOperationException("Deserialization failed.");
        return new Envelope<T>(dto);
    }
}
