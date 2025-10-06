#region

using System.Collections.Frozen;
using System.Diagnostics;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Core.Results.Validations;
using MessagePack;

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
    private readonly FrozenDictionary<string, object> _headers;
    private readonly IResultTelemetry _telemetry;

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

        if (IsSealed && string.IsNullOrWhiteSpace(Signature))
        {
            errors.Add(new ValidationError("Sealed envelope must have a signature"));
        }

        // Validate payload if validator provided and payload exists
        if (payloadValidator is not null && Payload is not null)
        {
            var payloadResult = payloadValidator(Payload);
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
        Payload = payload;
        _headers = headers;
        IsSealed = isSealed;
        CreatedAt = createdAt;
        SealedAt = sealedAt;
        Signature = signature;
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

        Payload = payload;
        _headers = headers?.ToFrozenDictionary(StringComparer.Ordinal)
                   ?? FrozenDictionary<string, object>.Empty;
        IsSealed = false;
        CreatedAt = DateTime.UtcNow;
        SealedAt = null;
        Signature = null;
        _telemetry = telemetry ?? TelemetryProvider.Current;

        _telemetry.TrackResultCreated(ResultState.Pending, typeof(Envelope<T>));
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the payload contained in this envelope.
    /// </summary>
    public T? Payload { get; }

    /// <summary>
    ///     Gets whether this envelope has a non-null payload.
    /// </summary>
    public bool HasPayload => Payload is not null;

    /// <summary>
    ///     Gets the headers associated with this envelope.
    /// </summary>
    public IReadOnlyDictionary<string, object> Headers => _headers;

    /// <summary>
    ///     Gets whether this envelope is sealed (immutable and signed).
    /// </summary>
    public bool IsSealed { get; }

    /// <summary>
    ///     Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    ///     Gets the timestamp when this envelope was sealed, if applicable.
    /// </summary>
    public DateTime? SealedAt { get; }

    /// <summary>
    ///     Gets the digital signature of this envelope, if sealed.
    /// </summary>
    public string? Signature { get; }

    /// <summary>
    ///     Gets the age of this envelope.
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - CreatedAt;

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
    public static EnvelopeBuilder<T> CreateBuilder() => new();

    /// <summary>
    ///     Creates a builder initialized with this envelope's data.
    /// </summary>
    public EnvelopeBuilder<T> ToBuilder()
    {
        var builder = new EnvelopeBuilder<T>();

        if (HasPayload && Payload is { } payload)
        {
            builder.WithPayload(payload);
        }

        builder.WithTelemetry(_telemetry);

        foreach (var (key, value) in _headers)
        {
            builder.WithHeader(key, value);
        }

        return builder;
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

        var newPayload = mapper(Payload!);

        return new Envelope<TResult>(
            newPayload,
            _headers,
            IsSealed,
            CreatedAt,
            SealedAt,
            Signature,
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

        var newPayload = await mapperAsync(Payload!).ConfigureAwait(false);

        return new Envelope<TResult>(
            newPayload,
            _headers,
            IsSealed,
            CreatedAt,
            SealedAt,
            Signature,
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

        if (IsSealed)
        {
            throw new InvalidOperationException("Envelope is already sealed");
        }

        if (!HasPayload)
        {
            throw new InvalidOperationException("Cannot seal an envelope without a payload");
        }

        var signature = signatureGenerator(Payload!);

        return new Envelope<T>(
            Payload,
            _headers,
            true,
            CreatedAt,
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

        if (!IsSealed)
        {
            return Result<Unit, ValidationError>.Failure(
                new ValidationError("Envelope is not sealed"));
        }

        if (!HasPayload || Signature is null)
        {
            return Result<Unit, ValidationError>.Failure(
                new ValidationError("Invalid envelope state for verification"));
        }

        var isValid = verifier(Payload!, Signature);

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
            Payload = Payload,
            Headers = _headers.ToDictionary(StringComparer.Ordinal),
            IsSealed = IsSealed,
            CreatedAt = CreatedAt,
            SealedAt = SealedAt,
            Signature = Signature
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
            telemetry ?? TelemetryProvider.Current);
    }

    /// <summary>
    ///     DTO for envelope serialization.
    /// </summary>
    [MessagePackObject]
    public record EnvelopeDto<TPayload>
    {
        [Key(0)] public TPayload? Payload { get; init; }

        [Key(1)] public Dictionary<string, object>? Headers { get; init; }

        [Key(2)] public bool IsSealed { get; init; }

        [Key(3)] public DateTime CreatedAt { get; init; }

        [Key(4)] public DateTime? SealedAt { get; init; }

        [Key(5)] public string? Signature { get; init; }
    }

    #endregion
}
