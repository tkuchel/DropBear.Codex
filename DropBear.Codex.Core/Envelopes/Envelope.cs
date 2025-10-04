#region

using System.Collections.Frozen;
using System.Diagnostics;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Envelopes;

/// <summary>
///     A secure, immutable envelope for wrapping and tracking data payloads.
///     Optimized for .NET 9 with modern C# features.
/// </summary>
/// <typeparam name="T">The type of the payload.</typeparam>
[DebuggerDisplay("Sealed = {IsSealed}, HasPayload = {HasPayload}")]
public sealed class Envelope<T>
{
    private readonly T? _payload;
    private readonly FrozenDictionary<string, object> _headers;
    private readonly bool _isSealed;
    private readonly DateTime _createdAt;
    private readonly DateTime? _sealedAt;
    private readonly string? _signature;
    private readonly IResultTelemetry _telemetry;

    #region Constructors

    /// <summary>
    ///     Internal constructor used by builder.
    /// </summary>
    internal Envelope(
        T? payload,
        FrozenDictionary<string, object> headers,
        bool isSealed,
        DateTime createdAt,
        DateTime? sealedAt,
        string? signature,
        IResultTelemetry telemetry)
    {
        _payload = payload;
        _headers = headers;
        _isSealed = isSealed;
        _createdAt = createdAt;
        _sealedAt = sealedAt;
        _signature = signature;
        _telemetry = telemetry;

        _telemetry.TrackResultCreated(
            isSealed ? ResultState.Success : ResultState.Pending,
            typeof(Envelope<T>));
    }

    /// <summary>
    ///     Creates a new envelope with the specified payload.
    ///     Uses modern null-checking patterns.
    /// </summary>
    public Envelope(
        T payload,
        IReadOnlyDictionary<string, object>? headers = null,
        IResultTelemetry? telemetry = null)
    {
        ArgumentNullException.ThrowIfNull(payload);

        _payload = payload;
        _headers = headers?.ToFrozenDictionary(StringComparer.Ordinal)
                   ?? FrozenDictionary<string, object>.Empty;
        _isSealed = false;
        _createdAt = DateTime.UtcNow;
        _sealedAt = null;
        _signature = null;
        _telemetry = telemetry ?? new DefaultResultTelemetry();

        _telemetry.TrackResultCreated(ResultState.Pending, typeof(Envelope<T>));
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the payload contained in this envelope.
    /// </summary>
    public T? Payload => _payload;

    /// <summary>
    ///     Gets whether this envelope has a non-null payload.
    /// </summary>
    public bool HasPayload => _payload is not null;

    /// <summary>
    ///     Gets the headers associated with this envelope.
    /// </summary>
    public IReadOnlyDictionary<string, object> Headers => _headers;

    /// <summary>
    ///     Gets whether this envelope is sealed (immutable and signed).
    /// </summary>
    public bool IsSealed => _isSealed;

    /// <summary>
    ///     Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    ///     Gets the timestamp when this envelope was sealed, if applicable.
    /// </summary>
    public DateTime? SealedAt => _sealedAt;

    /// <summary>
    ///     Gets the digital signature of this envelope, if sealed.
    /// </summary>
    public string? Signature => _signature;

    /// <summary>
    ///     Gets the age of this envelope.
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - _createdAt;

    #endregion

    #region Header Operations

    /// <summary>
    ///     Tries to get a header value by key.
    ///     Uses modern pattern matching for type safety.
    /// </summary>
    public bool TryGetHeader<TValue>(string key, out TValue? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_headers.TryGetValue(key, out var obj) && obj is TValue typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    ///     Gets a header value by key, or returns a default value.
    /// </summary>
    public TValue? GetHeaderOrDefault<TValue>(string key, TValue? defaultValue = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return TryGetHeader<TValue>(key, out var value) ? value : defaultValue;
    }

    #endregion

    #region Builder Pattern

    /// <summary>
    ///     Creates a builder for constructing envelopes with a fluent API.
    /// </summary>
    public static EnvelopeBuilder<T> CreateBuilder()
    {
        return new EnvelopeBuilder<T>();
    }

    /// <summary>
    ///     Creates a builder initialized with this envelope's data.
    /// </summary>
    public EnvelopeBuilder<T> ToBuilder()
    {
        var builder = new EnvelopeBuilder<T>()
            .WithPayload(_payload!);

        foreach (var (key, value) in _headers)
        {
            builder.WithHeader(key, value);
        }

        return builder;
    }

    #endregion

    #region Validation

    /// <summary>
    ///     Validates the envelope and its payload.
    /// </summary>
    public ValidationResult Validate(Func<T, ValidationResult>? payloadValidator = null)
    {
        var errors = new List<ValidationError>();

        // Validate envelope state
        if (!HasPayload)
        {
            errors.Add(ValidationError.Required(nameof(Payload)));
        }

        if (_isSealed && string.IsNullOrWhiteSpace(_signature))
        {
            errors.Add(new ValidationError("Sealed envelope must have a signature"));
        }

        // Validate payload if validator provided and payload exists
        if (payloadValidator is not null && _payload is not null)
        {
            var payloadResult = payloadValidator(_payload);
            if (!payloadResult.IsValid)
            {
                errors.AddRange(payloadResult.Errors);
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success
            : ValidationResult.Failed(errors);
    }

    #endregion

    #region Transformation

    /// <summary>
    ///     Maps the payload to a new type, creating a new envelope.
    ///     Uses modern expression-bodied members.
    /// </summary>
    public Envelope<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        if (!HasPayload)
        {
            throw new InvalidOperationException("Cannot map an envelope without a payload");
        }

        var newPayload = mapper(_payload!);

        return new Envelope<TResult>(
            newPayload,
            _headers,
            _isSealed,
            _createdAt,
            _sealedAt,
            _signature,
            _telemetry);
    }

    /// <summary>
    ///     Maps the payload asynchronously to a new type.
    /// </summary>
    public async ValueTask<Envelope<TResult>> MapAsync<TResult>(
        Func<T, ValueTask<TResult>> mapperAsync)
    {
        ArgumentNullException.ThrowIfNull(mapperAsync);

        if (!HasPayload)
        {
            throw new InvalidOperationException("Cannot map an envelope without a payload");
        }

        var newPayload = await mapperAsync(_payload!).ConfigureAwait(false);

        return new Envelope<TResult>(
            newPayload,
            _headers,
            _isSealed,
            _createdAt,
            _sealedAt,
            _signature,
            _telemetry);
    }

    #endregion

    #region Sealing

    /// <summary>
    ///     Seals this envelope with a digital signature, making it immutable.
    /// </summary>
    public Envelope<T> Seal(Func<T, string> signatureGenerator)
    {
        ArgumentNullException.ThrowIfNull(signatureGenerator);

        if (_isSealed)
        {
            throw new InvalidOperationException("Envelope is already sealed");
        }

        if (!HasPayload)
        {
            throw new InvalidOperationException("Cannot seal an envelope without a payload");
        }

        var signature = signatureGenerator(_payload!);

        return new Envelope<T>(
            _payload,
            _headers,
            isSealed: true,
            _createdAt,
            DateTime.UtcNow,
            signature,
            _telemetry);
    }

    /// <summary>
    ///     Verifies the signature of a sealed envelope.
    /// </summary>
    public Result<Unit, ValidationError> VerifySignature(Func<T, string, bool> verifier)
    {
        ArgumentNullException.ThrowIfNull(verifier);

        if (!_isSealed)
        {
            return Result<Unit, ValidationError>.Failure(
                new ValidationError("Envelope is not sealed"));
        }

        if (!HasPayload || _signature is null)
        {
            return Result<Unit, ValidationError>.Failure(
                new ValidationError("Invalid envelope state for verification"));
        }

        var isValid = verifier(_payload!, _signature);

        return isValid
            ? Result<Unit, ValidationError>.Success(Unit.Value)
            : Result<Unit, ValidationError>.Failure(
                new ValidationError("Signature verification failed"));
    }

    #endregion

    #region Serialization Support

    /// <summary>
    ///     Gets a DTO representation for serialization.
    ///     Uses modern record pattern for clean data transfer.
    /// </summary>
    public EnvelopeDto<T> GetDto()
    {
        return new EnvelopeDto<T>
        {
            Payload = _payload,
            Headers = _headers.ToDictionary(StringComparer.Ordinal),
            IsSealed = _isSealed,
            CreatedAt = _createdAt,
            SealedAt = _sealedAt,
            Signature = _signature
        };
    }

    /// <summary>
    ///     Creates an envelope from a DTO.
    /// </summary>
    public static Envelope<T> FromDto(EnvelopeDto<T> dto, IResultTelemetry? telemetry = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new Envelope<T>(
            dto.Payload,
            dto.Headers?.ToFrozenDictionary(StringComparer.Ordinal) ?? FrozenDictionary<string, object>.Empty,
            dto.IsSealed,
            dto.CreatedAt,
            dto.SealedAt,
            dto.Signature,
            telemetry ?? new DefaultResultTelemetry());
    }

    /// <summary>
    ///     DTO for envelope serialization.
    /// </summary>
    public record EnvelopeDto<TPayload>
    {
        public TPayload? Payload { get; init; }
        public Dictionary<string, object>? Headers { get; init; }
        public bool IsSealed { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? SealedAt { get; init; }
        public string? Signature { get; init; }
    }

    #endregion
}
