#region

using System.Collections.Frozen;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Diagnostics;

#endregion

namespace DropBear.Codex.Core.Envelopes;

/// <summary>
///     Fluent builder for constructing envelopes.
///     Optimized for .NET 10 with modern builder patterns.
/// </summary>
public sealed class EnvelopeBuilder<T>
{
    private readonly Dictionary<string, object> _headers = new(StringComparer.Ordinal);
    private T? _payload;
    private IResultTelemetry? _telemetry;

    /// <summary>
    ///     Sets the payload for the envelope.
    /// </summary>
    public EnvelopeBuilder<T> WithPayload(T payload)
    {
        _payload = payload;
        return this;
    }

    /// <summary>
    ///     Adds a header to the envelope.
    /// </summary>
    public EnvelopeBuilder<T> WithHeader(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        _headers[key] = value;
        return this;
    }

    /// <summary>
    ///     Adds multiple headers to the envelope.
    /// </summary>
    public EnvelopeBuilder<T> WithHeaders(params KeyValuePair<string, object>[] headers) =>
        WithHeaders((IEnumerable<KeyValuePair<string, object>>)headers);

    /// <summary>
    ///     Adds multiple headers to the envelope from an enumerable source.
    /// </summary>
    public EnvelopeBuilder<T> WithHeaders(IEnumerable<KeyValuePair<string, object>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        foreach (var (key, value) in headers)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Header keys cannot be null or whitespace.", nameof(headers));
            }

            if (value is null)
            {
                throw new ArgumentNullException(nameof(headers), "Header values cannot be null.");
            }

            _headers[key] = value;
        }

        return this;
    }

    /// <summary>
    ///     Sets the telemetry instance for the envelope.
    /// </summary>
    public EnvelopeBuilder<T> WithTelemetry(IResultTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        _telemetry = telemetry;
        return this;
    }

    /// <summary>
    ///     Builds the envelope.
    /// </summary>
    public Envelope<T> Build()
    {
        if (_payload is null)
        {
            throw new InvalidOperationException("Payload is required");
        }

        return new Envelope<T>(
            _payload,
            _headers.ToFrozenDictionary(StringComparer.Ordinal),
            false,
            DateTime.UtcNow,
            null,
            null,
            _telemetry ?? TelemetryProvider.Current);
    }

    /// <summary>
    ///     Builds and seals the envelope with a signature.
    /// </summary>
    public Envelope<T> BuildAndSeal(Func<Envelope<T>, string> signatureGenerator)
    {
        ArgumentNullException.ThrowIfNull(signatureGenerator);

        if (_payload is null)
        {
            throw new InvalidOperationException("Payload is required");
        }

        var createdAt = DateTime.UtcNow;
        var sealedAt = DateTime.UtcNow;
        var signableEnvelope = new Envelope<T>(
            _payload,
            _headers.ToFrozenDictionary(StringComparer.Ordinal),
            true,
            createdAt,
            sealedAt,
            null,
            _telemetry ?? TelemetryProvider.Current);
        var signature = signatureGenerator(signableEnvelope);

        return new Envelope<T>(
            _payload,
            signableEnvelope.Headers.ToFrozenDictionary(StringComparer.Ordinal),
            true,
            createdAt,
            sealedAt,
            signature,
            _telemetry ?? TelemetryProvider.Current);
    }
}
