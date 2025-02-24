#region

using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Envelopes;

/// <summary>
///     A composite envelope that encapsulates a collection of payloads.
///     Useful for batch processing.
/// </summary>
/// <typeparam name="T">Type of the individual payload.</typeparam>
public class CompositeEnvelope<T> : Envelope<IList<T>>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CompositeEnvelope{T}" /> class.
    /// </summary>
    /// <param name="payloads">The list of payloads.</param>
    /// <param name="headers">Optional initial headers.</param>
    /// <param name="telemetry">Optional telemetry service.</param>
    public CompositeEnvelope(
        IList<T> payloads,
        IDictionary<string, object>? headers = null,
        IResultTelemetry? telemetry = null)
        : base(payloads, telemetry)
    {
        if (headers != null)
        {
            foreach (var header in headers)
            {
                AddHeader(header.Key, header.Value);
            }
        }
    }

    /// <summary>
    ///     Internal constructor for deserialization.
    /// </summary>
    /// <param name="dto">The DTO used for deserialization.</param>
    /// <param name="telemetry">Optional telemetry service.</param>
    internal CompositeEnvelope(
        EnvelopeDto<IList<T>> dto,
        IResultTelemetry? telemetry = null)
        : base(dto, telemetry)
    {
    }

    /// <summary>
    ///     Adds a payload to the composite envelope.
    /// </summary>
    /// <param name="payload">The payload to add.</param>
    /// <returns>A Result indicating the success of adding the payload.</returns>
    public Result<Unit, ValidationError> AddPayload(T payload)
    {
        var newPayloads = new List<T>(Payload) { payload };
        var result = TryModifyPayload(newPayloads);

        return result.IsSuccess
            ? Result<Unit, ValidationError>.Success(Unit.Value)
            : Result<Unit, ValidationError>.Failure(result.Error!);
    }

    /// <summary>
    ///     Removes a payload from the composite envelope.
    /// </summary>
    /// <param name="payload">The payload to remove.</param>
    /// <returns>A Result indicating the success of removing the payload.</returns>
    public Result<Unit, ValidationError> RemovePayload(T payload)
    {
        var newPayloads = Payload.Where(p => !EqualityComparer<T>.Default.Equals(p, payload)).ToList();
        var result = TryModifyPayload(newPayloads);

        return result.IsSuccess
            ? Result<Unit, ValidationError>.Success(Unit.Value)
            : Result<Unit, ValidationError>.Failure(result.Error!);
    }

    /// <summary>
    ///     Filters payloads based on a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to filter payloads.</param>
    /// <returns>A Result containing a new CompositeEnvelope with filtered payloads.</returns>
    public Result<CompositeEnvelope<T>, ValidationError> FilterPayloads(Func<T, bool> predicate)
    {
        try
        {
            var filteredPayloads = Payload.Where(predicate).ToList();
            var filteredEnvelope = new CompositeEnvelope<T>(filteredPayloads);

            return Result<CompositeEnvelope<T>, ValidationError>.Success(filteredEnvelope);
        }
        catch (Exception ex)
        {
            return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                new ValidationError($"Payload filtering failed: {ex.Message}")
            );
        }
    }

    /// <summary>
    ///     Performs a batch operation on all payloads.
    /// </summary>
    /// <param name="processingFunc">The processing function to apply to each payload.</param>
    /// <returns>A Result indicating the success of the batch operation.</returns>
    public Result<Unit, ValidationError> ProcessPayloads(Func<T, Result<Unit, ValidationError>> processingFunc)
    {
        var results = Payload.Select(processingFunc).ToList();
        var failedResults = results.Where(r => !r.IsSuccess).ToList();

        if (failedResults.Any())
        {
            return Result<Unit, ValidationError>.Failure(
                new ValidationError($"Batch processing failed. {failedResults.Count} items failed.")
            );
        }

        return Result<Unit, ValidationError>.Success(Unit.Value);
    }
}
