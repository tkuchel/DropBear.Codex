#region

using System.Collections.Frozen;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Diagnostics;

#endregion

namespace DropBear.Codex.Core.Envelopes;

/// <summary>
///     Fluent builder for constructing composite envelopes.
///     Optimized for .NET 9 with modern builder patterns.
/// </summary>
public sealed class CompositeEnvelopeBuilder<T>
{
    private readonly Dictionary<string, object> _headers = new(StringComparer.Ordinal);
    private readonly List<T> _payloads = [];
    private IResultTelemetry? _telemetry;

    /// <summary>
    ///     Adds a single payload to the composite envelope.
    /// </summary>
    public CompositeEnvelopeBuilder<T> AddPayload(T payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _payloads.Add(payload);
        return this;
    }

    /// <summary>
    ///     Adds multiple payloads to the composite envelope.
    /// </summary>
    /// <remarks>
    ///     Uses params ReadOnlySpan for zero-allocation when called with inline arguments.
    /// </remarks>
    public CompositeEnvelopeBuilder<T> AddPayloads(params ReadOnlySpan<T> payloads)
    {
        foreach (var payload in payloads)
        {
            ArgumentNullException.ThrowIfNull(payload);
            _payloads.Add(payload);
        }

        return this;
    }

    /// <summary>
    ///     Adds multiple payloads from an enumerable source.
    /// </summary>
    public CompositeEnvelopeBuilder<T> AddPayloads(IEnumerable<T> payloads)
    {
        ArgumentNullException.ThrowIfNull(payloads);

        foreach (var payload in payloads)
        {
            ArgumentNullException.ThrowIfNull(payload);
            _payloads.Add(payload);
        }

        return this;
    }

    /// <summary>
    ///     Adds a header to the composite envelope.
    /// </summary>
    public CompositeEnvelopeBuilder<T> WithHeader(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        _headers[key] = value;
        return this;
    }

    /// <summary>
    ///     Adds multiple headers to the composite envelope.
    /// </summary>
    /// <remarks>
    ///     Uses params ReadOnlySpan for zero-allocation when called with inline arguments.
    /// </remarks>
    public CompositeEnvelopeBuilder<T> WithHeaders(params ReadOnlySpan<KeyValuePair<string, object>> headers)
    {
        foreach (var (key, value) in headers)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            _headers[key] = value;
        }

        return this;
    }

    /// <summary>
    ///     Adds multiple headers from an enumerable source.
    /// </summary>
    public CompositeEnvelopeBuilder<T> WithHeaders(IEnumerable<KeyValuePair<string, object>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        foreach (var (key, value) in headers)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            _headers[key] = value;
        }

        return this;
    }

    /// <summary>
    ///     Sets the telemetry instance for the composite envelope.
    /// </summary>
    public CompositeEnvelopeBuilder<T> WithTelemetry(IResultTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        _telemetry = telemetry;
        return this;
    }

    /// <summary>
    ///     Builds the composite envelope.
    /// </summary>
    public CompositeEnvelope<T> Build()
    {
        if (_payloads.Count == 0)
        {
            throw new InvalidOperationException("At least one payload is required");
        }

        return new CompositeEnvelope<T>(
            _payloads.ToFrozenSet(),
            _headers.ToFrozenDictionary(StringComparer.Ordinal),
            false,
            DateTime.UtcNow,
            null,
            null,
            _telemetry ?? TelemetryProvider.Current);
    }

    /// <summary>
    ///     Builds and seals the composite envelope with a signature.
    /// </summary>
    public CompositeEnvelope<T> BuildAndSeal(Func<IEnumerable<T>, string> signatureGenerator)
    {
        ArgumentNullException.ThrowIfNull(signatureGenerator);

        if (_payloads.Count == 0)
        {
            throw new InvalidOperationException("At least one payload is required");
        }

        var signature = signatureGenerator(_payloads);


        return new CompositeEnvelope<T>(
            _payloads.ToFrozenSet(),
            _headers.ToFrozenDictionary(StringComparer.Ordinal),
            true,
            DateTime.UtcNow,
            DateTime.UtcNow,
            signature,
            _telemetry ?? TelemetryProvider.Current);
    }
}
