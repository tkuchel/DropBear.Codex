#region

using System.Collections.Frozen;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Envelopes;

/// <summary>
///     A composite envelope that encapsulates a collection of payloads.
///     Immutable and thread-safe by design.
///     Optimized for .NET 9 with batch processing support.
/// </summary>
/// <typeparam name="T">Type of the individual payload.</typeparam>
public sealed class CompositeEnvelope<T>
{
    private readonly FrozenSet<T> _payloads;
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
    internal CompositeEnvelope(
        FrozenSet<T> payloads,
        FrozenDictionary<string, object> headers,
        bool isSealed,
        DateTime createdAt,
        DateTime? sealedAt,
        string? signature,
        IResultTelemetry telemetry)
    {
        _payloads = payloads;
        _headers = headers;
        _isSealed = isSealed;
        _createdAt = createdAt;
        _sealedAt = sealedAt;
        _signature = signature;
        _telemetry = telemetry;

        _telemetry.TrackResultCreated(
            isSealed ? ResultState.Success : ResultState.Pending,
            typeof(CompositeEnvelope<T>));
    }

    /// <summary>
    ///     Creates a new composite envelope with the specified payloads.
    ///     Uses collection expressions for modern syntax.
    /// </summary>
    public CompositeEnvelope(
        IEnumerable<T> payloads,
        IReadOnlyDictionary<string, object>? headers = null,
        IResultTelemetry? telemetry = null)
    {
        ArgumentNullException.ThrowIfNull(payloads);

        var payloadList = payloads.ToList();
        if (payloadList.Count == 0)
        {
            throw new ArgumentException("Payloads collection cannot be empty", nameof(payloads));
        }

        _payloads = payloadList.ToFrozenSet();
        _headers = headers?.ToFrozenDictionary(StringComparer.Ordinal)?? FrozenDictionary<string, object>.Empty;
        _isSealed = false;
        _createdAt = DateTime.UtcNow;
        _sealedAt = null;
        _signature = null;
        _telemetry = telemetry ?? new DefaultResultTelemetry();

        _telemetry.TrackResultCreated(ResultState.Pending, typeof(CompositeEnvelope<T>));
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the payloads as a read-only collection.
    /// </summary>
    public IReadOnlyCollection<T> Payloads => _payloads;

    /// <summary>
    ///     Gets the number of payloads in this envelope.
    /// </summary>
    public int Count => _payloads.Count;

    /// <summary>
    ///     Gets the headers associated with this envelope.
    /// </summary>
    public IReadOnlyDictionary<string, object> Headers => _headers;

    /// <summary>
    ///     Gets whether this envelope is sealed.
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

    #region Processing Operations

    /// <summary>
    ///     Processes all payloads synchronously.
    ///     Uses modern collection expressions for error aggregation.
    /// </summary>
    public Result<Unit, ValidationError> ProcessPayloads(
        Func<T, Result<Unit, ValidationError>> processingFunc)
    {
        ArgumentNullException.ThrowIfNull(processingFunc);

        var errors = new List<ValidationError>();

        foreach (var payload in _payloads)
        {
            var result = processingFunc(payload);
            if (!result.IsSuccess && result.Error is not null)
            {
                errors.Add(result.Error);
            }
        }

        if (errors.Count > 0)
        {
            var combinedMessage = string.Join("; ", errors.Select(e => e.Message));
            return Result<Unit, ValidationError>.Failure(
                new ValidationError($"Processing failed for {errors.Count} payloads: {combinedMessage}"));
        }

        return Result<Unit, ValidationError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Processes all payloads asynchronously.
    /// </summary>
    public async ValueTask<Result<Unit, ValidationError>> ProcessPayloadsAsync(
        Func<T, CancellationToken, ValueTask<Result<Unit, ValidationError>>> processingFunc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processingFunc);

        var errors = new List<ValidationError>();

        foreach (var payload in _payloads)
        {
            var result = await processingFunc(payload, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess && result.Error is not null)
            {
                errors.Add(result.Error);
            }
        }

        if (errors.Count > 0)
        {
            var combinedMessage = string.Join("; ", errors.Select(e => e.Message));
            return Result<Unit, ValidationError>.Failure(
                new ValidationError($"Processing failed for {errors.Count} payloads: {combinedMessage}"));
        }

        return Result<Unit, ValidationError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Processes all payloads in parallel with controlled degree of parallelism.
    ///     Uses modern Parallel.ForAsync for optimal performance.
    /// </summary>
    public async ValueTask<Result<Unit, ValidationError>> ProcessPayloadsParallelAsync(
        Func<T, CancellationToken, ValueTask<Result<Unit, ValidationError>>> processingFunc,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processingFunc);

        var payloadArray = _payloads.ToArray();
        var results = new Result<Unit, ValidationError>[payloadArray.Length];

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForAsync(0, payloadArray.Length, options, async (i, ct) =>
        {
            results[i] = await processingFunc(payloadArray[i], ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var errors = results
            .Where(r => !r.IsSuccess && r.Error is not null)
            .Select(r => r.Error!)
            .ToList();

        if (errors.Count > 0)
        {
            var combinedMessage = string.Join("; ", errors.Select(e => e.Message));
            return Result<Unit, ValidationError>.Failure(
                new ValidationError($"Processing failed for {errors.Count} payloads: {combinedMessage}"));
        }

        return Result<Unit, ValidationError>.Success(Unit.Value);
    }

    #endregion

    #region Transformation

    /// <summary>
    ///     Maps all payloads to a new type, creating a new composite envelope.
    /// </summary>
    public CompositeEnvelope<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        var mappedPayloads = _payloads.Select(mapper).ToFrozenSet();

        return new CompositeEnvelope<TResult>(
            mappedPayloads,
            _headers,
            _isSealed,
            _createdAt,
            _sealedAt,
            _signature,
            _telemetry);
    }

    /// <summary>
    ///     Maps all payloads asynchronously to a new type.
    /// </summary>
    public async ValueTask<CompositeEnvelope<TResult>> MapAsync<TResult>(
        Func<T, ValueTask<TResult>> mapperAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapperAsync);

        var mappedPayloads = new List<TResult>(_payloads.Count);

        foreach (var payload in _payloads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapped = await mapperAsync(payload).ConfigureAwait(false);
            mappedPayloads.Add(mapped);
        }

        return new CompositeEnvelope<TResult>(
            mappedPayloads.ToFrozenSet(),
            _headers,
            _isSealed,
            _createdAt,
            _sealedAt,
            _signature,
            _telemetry);
    }

    /// <summary>
    ///     Filters payloads based on a predicate.
    /// </summary>
    public CompositeEnvelope<T> Where(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var filteredPayloads = _payloads.Where(predicate).ToFrozenSet();

        if (filteredPayloads.Count == 0)
        {
            throw new InvalidOperationException("Filter would result in empty composite envelope");
        }

        return new CompositeEnvelope<T>(
            filteredPayloads,
            _headers,
            _isSealed,
            _createdAt,
            _sealedAt,
            _signature,
            _telemetry);
    }

    #endregion

    #region Batch Processing

    /// <summary>
    ///     Processes payloads in batches with configurable batch size.
    ///     Uses modern Chunk() method for efficient batching.
    /// </summary>
    public async ValueTask<Result<Unit, ValidationError>> ProcessInBatchesAsync(
        Func<IEnumerable<T>, CancellationToken, ValueTask<Result<Unit, ValidationError>>> batchProcessor,
        int batchSize = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batchProcessor);

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive");
        }

        var errors = new List<ValidationError>();

        foreach (var batch in _payloads.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await batchProcessor(batch, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess && result.Error is not null)
            {
                errors.Add(result.Error);
            }
        }

        if (errors.Count > 0)
        {
            var combinedMessage = string.Join("; ", errors.Select(e => e.Message));
            return Result<Unit, ValidationError>.Failure(
                new ValidationError($"Batch processing failed: {combinedMessage}"));
        }

        return Result<Unit, ValidationError>.Success(Unit.Value);
    }

    #endregion

    #region Sealing

    /// <summary>
    ///     Seals this composite envelope with a digital signature.
    /// </summary>
    public CompositeEnvelope<T> Seal(Func<IEnumerable<T>, string> signatureGenerator)
    {
        ArgumentNullException.ThrowIfNull(signatureGenerator);

        if (_isSealed)
        {
            throw new InvalidOperationException("Composite envelope is already sealed");
        }

        var signature = signatureGenerator(_payloads);

        return new CompositeEnvelope<T>(
            _payloads,
            _headers,
            isSealed: true,
            _createdAt,
            DateTime.UtcNow,
            signature,
            _telemetry);
    }

    /// <summary>
    ///     Verifies the signature of a sealed composite envelope.
    /// </summary>
    public Result<Unit, ValidationError> VerifySignature(Func<IEnumerable<T>, string, bool> verifier)
    {
        ArgumentNullException.ThrowIfNull(verifier);

        if (!_isSealed)
        {
            return Result<Unit, ValidationError>.Failure(
                new ValidationError("Composite envelope is not sealed"));
        }

        if (_signature is null)
        {
            return Result<Unit, ValidationError>.Failure(
                new ValidationError("Invalid envelope state for verification"));
        }

        var isValid = verifier(_payloads, _signature);

        return isValid
            ? Result<Unit, ValidationError>.Success(Unit.Value)
            : Result<Unit, ValidationError>.Failure(
                new ValidationError("Signature verification failed"));
    }

    #endregion

    #region Builder Pattern

    /// <summary>
    ///     Creates a builder for constructing composite envelopes with a fluent API.
    /// </summary>
    public static CompositeEnvelopeBuilder<T> CreateBuilder()
    {
        return new CompositeEnvelopeBuilder<T>();
    }

    #endregion
}

/// <summary>
///     Fluent builder for constructing composite envelopes.
///     Optimized for .NET 9 with modern builder patterns.
/// </summary>
public sealed class CompositeEnvelopeBuilder<T>
{
    private readonly List<T> _payloads = [];
    private readonly Dictionary<string, object> _headers = new(StringComparer.Ordinal);
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
    ///     Uses params collection for modern syntax.
    /// </summary>
    public CompositeEnvelopeBuilder<T> AddPayloads(params IEnumerable<T> payloads)
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
    public CompositeEnvelopeBuilder<T> WithHeaders(params IEnumerable<KeyValuePair<string, object>> headers)
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
            isSealed: false,
            DateTime.UtcNow,
            sealedAt: null,
            signature: null,
            _telemetry ?? new DefaultResultTelemetry());
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
            isSealed: true,
            DateTime.UtcNow,
            DateTime.UtcNow,
            signature,
            _telemetry ?? new DefaultResultTelemetry());
    }
}
