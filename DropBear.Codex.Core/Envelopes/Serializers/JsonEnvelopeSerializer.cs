#region

using System.Text;
using System.Text.Json;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Diagnostics;

#endregion

namespace DropBear.Codex.Core.Envelopes.Serializers;

/// <summary>
///     Provides JSON-based serialization for envelopes using System.Text.Json.
/// </summary>
public sealed class JsonEnvelopeSerializer : IEnvelopeSerializer
{
    /// <summary>
    ///     Default JSON serialization options for envelopes.
    /// </summary>
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true
        // Add any additional default serialization configurations
    };

    private readonly IResultTelemetry? _telemetry;

    // Optional constructor to inject telemetry
    public JsonEnvelopeSerializer(IResultTelemetry? telemetry = null)
    {
        _telemetry = telemetry ?? new DefaultResultTelemetry();
    }

    /// <inheritdoc />
    public string Serialize<T>(Envelope<T> envelope)
    {
        var dto = envelope.GetDto();
        return JsonSerializer.Serialize(dto, DefaultOptions);
    }

    /// <inheritdoc />
    public Envelope<T> Deserialize<T>(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            throw new ArgumentException("Serialized data cannot be null or empty.", nameof(data));
        }

        var dto = JsonSerializer.Deserialize<Envelope<T>.EnvelopeDto<T>>(data, DefaultOptions) ??
                  throw new JsonException("Failed to deserialize envelope");

        // Construct new envelope using the DTO constructor
        var envelope = new Envelope<T>(dto, _telemetry);
        return envelope;
    }

    /// <inheritdoc />
    public byte[] SerializeToBinary<T>(Envelope<T> envelope)
    {
        // Use JSON serialization as binary for consistent behavior
        var serializedString = Serialize(envelope);
        return Encoding.UTF8.GetBytes(serializedString);
    }

    /// <inheritdoc />
    public Envelope<T> DeserializeFromBinary<T>(byte[] data)
    {
        // Convert binary back to string for deserialization
        var serializedString = Encoding.UTF8.GetString(data);
        return Deserialize<T>(serializedString);
    }
}
