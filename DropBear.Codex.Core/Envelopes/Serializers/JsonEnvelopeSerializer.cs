#region

using System.Text;
using System.Text.Json;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Diagnostics;

#endregion

namespace DropBear.Codex.Core.Envelopes.Serializers;

/// <summary>
///     Provides JSON-based serialization for envelopes using System.Text.Json.
/// </summary>
/// <remarks>
///     By default, it uses <see cref="DefaultOptions" /> which has indentation enabled
///     (slightly impacting performance). For higher performance, consider providing
///     less expensive options (e.g. disabling indentation, disabling case-insensitive matching).
/// </remarks>
public sealed class JsonEnvelopeSerializer : IEnvelopeSerializer
{
    /// <summary>
    ///     Default JSON serialization options for envelopes.
    ///     <para>Set WriteIndented = false for better performance if human readability is not required.</para>
    /// </summary>
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true, // *** CHANGE *** Turn off if performance is more critical than readability
        PropertyNameCaseInsensitive = true // *** CHANGE *** Turn off if not needed
        // Add any additional default serialization configurations if required
    };

    private readonly IResultTelemetry _telemetry;

    /// <summary>
    ///     Initializes an instance of <see cref="JsonEnvelopeSerializer" /> with optional telemetry.
    /// </summary>
    /// <param name="telemetry">Optional telemetry interface for recording serialization diagnostics.</param>
    public JsonEnvelopeSerializer(IResultTelemetry? telemetry = null)
    {
        _telemetry = telemetry ?? new DefaultResultTelemetry();
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="envelope" /> is null.</exception>
    public string Serialize<T>(Envelope<T> envelope)
    {
        // *** CHANGE *** Defensive check for null envelope
        if (envelope == null)
        {
            throw new ArgumentNullException(nameof(envelope), "Envelope cannot be null.");
        }

        try
        {
            var dto = envelope.GetDto();
            return JsonSerializer.Serialize(dto, DefaultOptions);
        }
        catch (Exception ex)
        {
            // *** CHANGE *** Optionally log to telemetry or rethrow with context
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "Serialize");
            throw new InvalidOperationException("Failed to serialize envelope to JSON.", ex);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown if <paramref name="data" /> is null or whitespace.</exception>
    /// <exception cref="JsonException">Thrown if the JSON is invalid or deserialization fails.</exception>
    public Envelope<T> Deserialize<T>(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            throw new ArgumentException("Serialized data cannot be null or whitespace.", nameof(data));
        }

        try
        {
            var dto = JsonSerializer.Deserialize<Envelope<T>.EnvelopeDto<T>>(data, DefaultOptions);
            if (dto == null)
            {
                throw new JsonException("JSON deserialization returned null.");
            }

            return new Envelope<T>(dto, _telemetry);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // *** CHANGE *** Log to telemetry; wrap or rethrow with context
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "Deserialize");
            throw;
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "Deserialize");
            throw new JsonException("Failed to deserialize the envelope from JSON due to an unexpected error.", ex);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="envelope" /> is null.</exception>
    public byte[] SerializeToBinary<T>(Envelope<T> envelope)
    {
        // *** CHANGE *** Defensive check
        if (envelope == null)
        {
            throw new ArgumentNullException(nameof(envelope), "Envelope cannot be null.");
        }

        try
        {
            var serializedString = Serialize(envelope);
            return Encoding.UTF8.GetBytes(serializedString);
        }
        catch (Exception ex)
        {
            // Optionally log to telemetry or rethrow with more context
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "SerializeToBinary");
            throw;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown if <paramref name="data" /> is null or empty.</exception>
    public Envelope<T> DeserializeFromBinary<T>(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Binary data cannot be null or empty.", nameof(data));
        }

        try
        {
            var serializedString = Encoding.UTF8.GetString(data);
            return Deserialize<T>(serializedString);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "DeserializeFromBinary");
            throw;
        }
    }
}
