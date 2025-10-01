#region

using System.Collections.Frozen;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Envelopes;

/// <summary>
///     Builder for constructing envelopes with a fluent API.
///     Optimized for .NET 9 with validation and type safety.
/// </summary>
/// <typeparam name="T">The type of payload.</typeparam>
public sealed class EnvelopeBuilder<T>
{
    private T? _payload;
    private readonly Dictionary<string, object> _headers;
    private readonly List<Func<T, ValidationResult>> _payloadValidators;
    private readonly List<Func<string, object, ValidationResult>> _headerValidators;
    private IResultTelemetry _telemetry;

    /// <summary>
    ///     Initializes a new instance of EnvelopeBuilder.
    /// </summary>
    public EnvelopeBuilder()
    {
        _headers = new Dictionary<string, object>(StringComparer.Ordinal);
        _payloadValidators = new List<Func<T, ValidationResult>>();
        _headerValidators = new List<Func<string, object, ValidationResult>>();
        _telemetry = new DefaultResultTelemetry();
    }

    #region Payload Configuration

    /// <summary>
    ///     Sets the payload for the envelope.
    /// </summary>
    public EnvelopeBuilder<T> WithPayload(T payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _payload = payload;
        return this;
    }

    /// <summary>
    ///     Adds a payload validator that will be run during Build.
    /// </summary>
    public EnvelopeBuilder<T> WithPayloadValidator(Func<T, ValidationResult> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _payloadValidators.Add(validator);
        return this;
    }

    /// <summary>
    ///     Adds multiple payload validators.
    /// </summary>
    public EnvelopeBuilder<T> WithPayloadValidators(IEnumerable<Func<T, ValidationResult>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        _payloadValidators.AddRange(validators);
        return this;
    }

    #endregion

    #region Header Configuration

    /// <summary>
    ///     Adds a header to the envelope.
    /// </summary>
    public EnvelopeBuilder<T> AddHeader(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        _headers[key] = value;
        return this;
    }

    /// <summary>
    ///     Adds multiple headers to the envelope.
    /// </summary>
    public EnvelopeBuilder<T> AddHeaders(IReadOnlyDictionary<string, object> headers)
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
    ///     Adds a header validator that will be run during Build.
    /// </summary>
    public EnvelopeBuilder<T> WithHeaderValidator(Func<string, object, ValidationResult> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _headerValidators.Add(validator);
        return this;
    }

    /// <summary>
    ///     Adds multiple header validators.
    /// </summary>
    public EnvelopeBuilder<T> WithHeaderValidators(
        IEnumerable<Func<string, object, ValidationResult>> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        _headerValidators.AddRange(validators);
        return this;
    }

    #endregion

    #region Telemetry Configuration

    /// <summary>
    ///     Sets a custom telemetry instance for the envelope.
    /// </summary>
    public EnvelopeBuilder<T> WithTelemetry(IResultTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        _telemetry = telemetry;
        return this;
    }

    #endregion

    #region Common Header Helpers

    /// <summary>
    ///     Adds a correlation ID header for request tracking.
    ///     If correlationId is null, generates a new GUID.
    /// </summary>
    public EnvelopeBuilder<T> WithCorrelationId(string? correlationId = null)
    {
        var id = correlationId ?? Guid.NewGuid().ToString();
        return AddHeader("CorrelationId", id);
    }

    /// <summary>
    ///     Adds a source identifier header.
    /// </summary>
    public EnvelopeBuilder<T> WithSource(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return AddHeader("Source", source);
    }

    /// <summary>
    ///     Adds a timestamp header.
    /// </summary>
    public EnvelopeBuilder<T> WithTimestamp(DateTime? timestamp = null)
    {
        return AddHeader("Timestamp", timestamp ?? DateTime.UtcNow);
    }

    /// <summary>
    ///     Adds a content type header.
    /// </summary>
    public EnvelopeBuilder<T> WithContentType(string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        return AddHeader("ContentType", contentType);
    }

    /// <summary>
    ///     Adds a version header.
    /// </summary>
    public EnvelopeBuilder<T> WithVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return AddHeader("Version", version);
    }

    /// <summary>
    ///     Adds metadata as a header.
    /// </summary>
    public EnvelopeBuilder<T> WithMetadata(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        return AddHeader($"Metadata.{key}", value);
    }

    #endregion

    #region Build Methods

    /// <summary>
    ///     Builds the envelope, running all validators.
    /// </summary>
    public Result<Envelope<T>, ValidationError> Build()
    {
        // Validate payload is set
        if (_payload == null)
        {
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError("Payload is required"));
        }

        // Run payload validators
        var payloadValidationResult = ValidatePayload();
        if (!payloadValidationResult.IsValid)
        {
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError(payloadValidationResult.ErrorMessage));
        }

        // Run header validators
        var headerValidationResult = ValidateHeaders();
        if (!headerValidationResult.IsValid)
        {
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError(headerValidationResult.ErrorMessage));
        }

        // Create immutable envelope
        var envelope = new Envelope<T>(
            _payload,
            _headers.ToFrozenDictionary(StringComparer.Ordinal),
            isSealed: false,
            createdAt: DateTime.UtcNow,
            sealedAt: null,
            signature: null,
            encryptedPayload: null,
            _telemetry);

        return Result<Envelope<T>, ValidationError>.Success(envelope);
    }

    /// <summary>
    ///     Builds and seals the envelope in one step.
    /// </summary>
    public Result<Envelope<T>, ValidationError> BuildAndSeal(byte[]? cryptographicKey = null)
    {
        var buildResult = Build();
        if (!buildResult.IsSuccess)
        {
            return buildResult;
        }

        return buildResult.Value!.Seal(cryptographicKey);
    }

    /// <summary>
    ///     Builds, seals, and encrypts the envelope in one step.
    /// </summary>
    public Result<Envelope<T>, ValidationError> BuildSealAndEncrypt(
        byte[] encryptionKey,
        byte[]? signingKey = null)
    {
        ArgumentNullException.ThrowIfNull(encryptionKey);

        var buildResult = BuildAndSeal(signingKey);
        if (!buildResult.IsSuccess)
        {
            return buildResult;
        }

        return buildResult.Value!.Encrypt(encryptionKey);
    }

    #endregion

    #region Async Build Methods

    /// <summary>
    ///     Builds the envelope asynchronously, running async validators if any were added.
    /// </summary>
    public async ValueTask<Result<Envelope<T>, ValidationError>> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        // For now, just delegate to synchronous build
        // In the future, could support async validators
        return await ValueTask.FromResult(Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Builds and seals the envelope asynchronously.
    /// </summary>
    public async ValueTask<Result<Envelope<T>, ValidationError>> BuildAndSealAsync(
        byte[]? cryptographicKey = null,
        CancellationToken cancellationToken = default)
    {
        var buildResult = await BuildAsync(cancellationToken).ConfigureAwait(false);
        if (!buildResult.IsSuccess)
        {
            return buildResult;
        }

        return buildResult.Value!.Seal(cryptographicKey);
    }

    #endregion

    #region Validation Helpers

    private ValidationResult ValidatePayload()
    {
        if (_payloadValidators.Count == 0)
        {
            return ValidationResult.Success;
        }

        var results = _payloadValidators
            .Select(validator => validator(_payload!))
            .ToList();

        return ValidationResult.Combine(results);
    }

    private ValidationResult ValidateHeaders()
    {
        if (_headerValidators.Count == 0)
        {
            return ValidationResult.Success;
        }

        var results = new List<ValidationResult>();

        foreach (var (key, value) in _headers)
        {
            foreach (var validator in _headerValidators)
            {
                results.Add(validator(key, value));
            }
        }

        return ValidationResult.Combine(results);
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a builder from an existing envelope (for modification).
    /// </summary>
    public static EnvelopeBuilder<T> FromEnvelope(Envelope<T> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.IsSealed)
        {
            throw new InvalidOperationException("Cannot create builder from sealed envelope");
        }

        var builder = new EnvelopeBuilder<T>()
            .WithPayload(envelope.Payload);

        foreach (var (key, value) in envelope.Headers)
        {
            builder.AddHeader(key, value);
        }

        return builder;
    }

    /// <summary>
    ///     Creates a builder with a payload already set.
    ///     Static factory method to avoid conflict with instance method.
    /// </summary>
    public static EnvelopeBuilder<T> Create(T payload)
    {
        return new EnvelopeBuilder<T>().WithPayload(payload);
    }

    #endregion

    #region Preset Configurations

    /// <summary>
    ///     Configures the builder with common headers for API requests.
    /// </summary>
    public EnvelopeBuilder<T> ConfigureForApiRequest(
        string? correlationId = null,
        string? source = null)
    {
        WithCorrelationId(correlationId)
            .WithTimestamp()
            .WithContentType("application/json");

        if (!string.IsNullOrWhiteSpace(source))
        {
            WithSource(source);
        }

        return this;
    }

    /// <summary>
    ///     Configures the builder with common headers for events.
    /// </summary>
    public EnvelopeBuilder<T> ConfigureForEvent(
        string eventType,
        string? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        return WithCorrelationId(correlationId)
            .WithTimestamp()
            .AddHeader("EventType", eventType)
            .WithVersion("1.0");
    }

    /// <summary>
    ///     Configures the builder with common headers for messages.
    /// </summary>
    public EnvelopeBuilder<T> ConfigureForMessage(
        string messageType,
        string? correlationId = null,
        string? replyTo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);

        WithCorrelationId(correlationId)
            .WithTimestamp()
            .AddHeader("MessageType", messageType);

        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            AddHeader("ReplyTo", replyTo);
        }

        return this;
    }

    #endregion
}
