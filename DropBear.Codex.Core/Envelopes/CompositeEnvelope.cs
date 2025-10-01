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
        _headers = headers?.ToFrozenDictionary(StringComparer.Ordinal)
                   ?? FrozenDictionary<string, object>.Empty;
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
    public IReadOnlySet<T> Payloads => _payloads;

    /// <summary>
    ///     Gets the headers as a read-only collection.
    /// </summary>
    public IReadOnlyDictionary<string, object> Headers => _headers;

    /// <summary>
    ///     Gets the number of payloads in the envelope.
    /// </summary>
    public int Count => _payloads.Count;

    /// <summary>
    ///     Gets a value indicating whether the envelope is sealed.
    /// </summary>
    public bool IsSealed => _isSealed;

    /// <summary>
    ///     Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    ///     Gets the sealed timestamp if the envelope is sealed.
    /// </summary>
    public DateTime? SealedAt => _sealedAt;

    #endregion

    #region Modification Methods

    /// <summary>
    ///     Creates a new composite envelope with an additional payload.
    /// </summary>
    public Result<CompositeEnvelope<T>, ValidationError> AddPayload(T payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (_isSealed)
        {
            return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                new ValidationError("Cannot modify sealed envelope"));
        }

        var newPayloads = _payloads.Append(payload).ToFrozenSet();

        var newEnvelope = new CompositeEnvelope<T>(
            newPayloads,
            _headers,
            _isSealed,
            _createdAt,
            _sealedAt,
            _signature,
            _telemetry);

        return Result<CompositeEnvelope<T>, ValidationError>.Success(newEnvelope);
    }

    /// <summary>
    ///     Creates a new composite envelope with multiple additional payloads.
    /// </summary>
    public Result<CompositeEnvelope<T>, ValidationError> AddPayloads(IEnumerable<T> payloads)
    {
        ArgumentNullException.ThrowIfNull(payloads);

        if (_isSealed)
        {
            return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                new ValidationError("Cannot modify sealed envelope"));
        }

        var newPayloads = _payloads.Concat(payloads).ToFrozenSet();

        var newEnvelope = new CompositeEnvelope<T>(
            newPayloads,
            _headers,
            _isSealed,
            _createdAt,
            _sealedAt,
            _signature,
            _telemetry);

        return Result<CompositeEnvelope<T>, ValidationError>.Success(newEnvelope);
    }

    /// <summary>
    ///     Creates a new composite envelope with a payload removed.
    /// </summary>
    public Result<CompositeEnvelope<T>, ValidationError> RemovePayload(T payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (_isSealed)
        {
            return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                new ValidationError("Cannot modify sealed envelope"));
        }

        if (!_payloads.Contains(payload))
        {
            return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                new ValidationError("Payload not found in envelope"));
        }

        var newPayloads = _payloads.Except([payload]).ToFrozenSet();

        if (newPayloads.Count == 0)
        {
            return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                new ValidationError("Cannot remove last payload from envelope"));
        }

        var newEnvelope = new CompositeEnvelope<T>(
            newPayloads,
            _headers,
            _isSealed,
            _createdAt,
            _sealedAt,
            _signature,
            _telemetry);

        return Result<CompositeEnvelope<T>, ValidationError>.Success(newEnvelope);
    }

    /// <summary>
    ///     Creates a new composite envelope with a header added.
    /// </summary>
    public Result<CompositeEnvelope<T>, ValidationError> WithHeader(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        if (_isSealed)
        {
            return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                new ValidationError("Cannot modify sealed envelope"));
        }

        var newHeaders = _headers.ToDictionary(StringComparer.Ordinal);
        newHeaders[key] = value;

        var newEnvelope = new CompositeEnvelope<T>(
            _payloads,
            newHeaders.ToFrozenDictionary(StringComparer.Ordinal),
            _isSealed,
            _createdAt,
            _sealedAt,
            _signature,
            _telemetry);

        return Result<CompositeEnvelope<T>, ValidationError>.Success(newEnvelope);
    }

    #endregion

    #region Filtering and Processing

    /// <summary>
    ///     Creates a new composite envelope with filtered payloads.
    /// </summary>
    public Result<CompositeEnvelope<T>, ValidationError> Filter(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        try
        {
            var filtered = _payloads.Where(predicate).ToFrozenSet();

            if (filtered.Count == 0)
            {
                return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                    new ValidationError("Filter would result in empty envelope"));
            }

            var newEnvelope = new CompositeEnvelope<T>(
                filtered,
                _headers,
                _isSealed,
                _createdAt,
                _sealedAt,
                _signature,
                _telemetry);

            return Result<CompositeEnvelope<T>, ValidationError>.Success(newEnvelope);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(CompositeEnvelope<T>));
            return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                new ValidationError($"Filtering failed: {ex.Message}"));
        }
    }

    /// <summary>
    ///     Processes all payloads with the provided function.
    /// </summary>
    public Result<Unit, ValidationError> ProcessPayloads(
        Func<T, Result<Unit, ValidationError>> processingFunc)
    {
        ArgumentNullException.ThrowIfNull(processingFunc);

        var errors = new List<ValidationError>();

        foreach (var payload in _payloads)
        {
            var result = processingFunc(payload);
            if (!result.IsSuccess && result.Error != null)
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
            if (!result.IsSuccess && result.Error != null)
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
    /// </summary>
    public async ValueTask<Result<Unit, ValidationError>> ProcessPayloadsParallelAsync(
        Func<T, CancellationToken, ValueTask<Result<Unit, ValidationError>>> processingFunc,
        int maxDegreeOfParallelism = 4,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processingFunc);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxDegreeOfParallelism, 0);

        var errors = new System.Collections.Concurrent.ConcurrentBag<ValidationError>();

        await Parallel.ForEachAsync(
            _payloads,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism, CancellationToken = cancellationToken
            },
            async (payload, ct) =>
            {
                var result = await processingFunc(payload, ct).ConfigureAwait(false);
                if (!result.IsSuccess && result.Error != null)
                {
                    errors.Add(result.Error);
                }
            }).ConfigureAwait(false);

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
    ///     Transforms the payloads to a new type.
    /// </summary>
    public Result<CompositeEnvelope<TResult>, ValidationError> Transform<TResult>(
        Func<T, TResult> transformer)
    {
        ArgumentNullException.ThrowIfNull(transformer);

        try
        {
            var transformed = _payloads.Select(transformer).ToFrozenSet();

            var newEnvelope = new CompositeEnvelope<TResult>(
                transformed,
                _headers,
                _isSealed,
                _createdAt,
                _sealedAt,
                _signature,
                _telemetry);

            return Result<CompositeEnvelope<TResult>, ValidationError>.Success(newEnvelope);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(CompositeEnvelope<T>));
            return Result<CompositeEnvelope<TResult>, ValidationError>.Failure(
                new ValidationError($"Transformation failed: {ex.Message}"));
        }
    }

    #endregion

    #region Sealing

    /// <summary>
    ///     Creates a new sealed composite envelope.
    /// </summary>
    public Result<CompositeEnvelope<T>, ValidationError> Seal(byte[]? cryptographicKey = null)
    {
        if (_isSealed)
        {
            return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                new ValidationError("Envelope is already sealed"));
        }

        string? signature = null;
        if (cryptographicKey != null)
        {
            // Compute signature for all payloads
            try
            {
                var dataForSigning = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Payloads = _payloads, Headers = _headers, CreatedAt = _createdAt
                });

                var dataBytes = System.Text.Encoding.UTF8.GetBytes(dataForSigning);
                using var hmac = new System.Security.Cryptography.HMACSHA256(cryptographicKey);
                var hash = hmac.ComputeHash(dataBytes);
                signature = Convert.ToBase64String(hash);
            }
            catch (Exception ex)
            {
                _telemetry.TrackException(ex, ResultState.Failure, typeof(CompositeEnvelope<T>));
                return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                    new ValidationError($"Signature computation failed: {ex.Message}"));
            }
        }

        var sealedEnvelope = new CompositeEnvelope<T>(
            _payloads,
            _headers,
            isSealed: true,
            _createdAt,
            sealedAt: DateTime.UtcNow,
            signature,
            _telemetry);

        _telemetry.TrackResultTransformed(
            ResultState.Pending,
            ResultState.Success,
            typeof(CompositeEnvelope<T>));

        return Result<CompositeEnvelope<T>, ValidationError>.Success(sealedEnvelope);
    }

    #endregion

    #region Conversion

    /// <summary>
    ///     Converts to individual envelopes.
    /// </summary>
    public IEnumerable<Envelope<T>> ToIndividualEnvelopes()
    {
        foreach (var payload in _payloads)
        {
            yield return new Envelope<T>(payload, _telemetry);
        }
    }

    /// <summary>
    ///     Converts to individual envelopes with shared headers.
    /// </summary>
    public IEnumerable<Envelope<T>> ToIndividualEnvelopesWithHeaders()
    {
        var headerDict = _headers.ToDictionary(StringComparer.Ordinal);

        foreach (var payload in _payloads)
        {
            var envelope = new Envelope<T>(payload, _telemetry);

            // Add all headers to each envelope
            var envelopeWithHeaders = envelope;
            foreach (var (key, value) in headerDict)
            {
                var result = envelopeWithHeaders.WithHeader(key, value);
                if (result.IsSuccess)
                {
                    envelopeWithHeaders = result.Value!;
                }
            }

            yield return envelopeWithHeaders;
        }
    }

    #endregion

    #region Partitioning

    /// <summary>
    ///     Partitions the composite envelope into smaller batches.
    /// </summary>
    public IEnumerable<CompositeEnvelope<T>> Partition(int batchSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

        var batches = _payloads.Chunk(batchSize);

        foreach (var batch in batches)
        {
            yield return new CompositeEnvelope<T>(
                batch.ToFrozenSet(),
                _headers,
                _isSealed,
                _createdAt,
                _sealedAt,
                _signature,
                _telemetry);
        }
    }

    #endregion

    #region Validation

    /// <summary>
    ///     Validates all payloads using the provided validator.
    /// </summary>
    public ValidationResult ValidateAll(Func<T, ValidationResult> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);

        var results = _payloads.Select(validator).ToList();
        return ValidationResult.Combine(results);
    }

    /// <summary>
    ///     Validates all payloads asynchronously.
    /// </summary>
    public async ValueTask<ValidationResult> ValidateAllAsync(
        Func<T, CancellationToken, ValueTask<ValidationResult>> validator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validator);

        var tasks = _payloads.Select(p => validator(p, cancellationToken).AsTask()).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        return ValidationResult.Combine(results);
    }

    #endregion
}
