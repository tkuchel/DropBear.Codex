#region

using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Envelopes;

/// <summary>
///     A composite envelope that encapsulates a collection of payloads.
///     Useful for batch processing scenarios.
/// </summary>
/// <typeparam name="T">Type of the individual payload.</typeparam>
public class CompositeEnvelope<T> : Envelope<IList<T>>
{

    private readonly IResultTelemetry? _telemetry;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CompositeEnvelope{T}" /> class
    ///     with the specified payload collection and optional headers/telemetry.
    /// </summary>
    /// <param name="payloads">The list of payloads. Must not be null.</param>
    /// <param name="headers">Optional initial headers to include in this envelope.</param>
    /// <param name="telemetry">Optional telemetry service for logging or diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="payloads" /> is null.
    /// </exception>
    public CompositeEnvelope(
        IList<T> payloads,
        IDictionary<string, object>? headers = null,
        IResultTelemetry? telemetry = null)
        : base(payloads ?? throw new ArgumentNullException(nameof(payloads)), telemetry)
    {
        if (headers != null)
        {
            foreach (var header in headers)
            {
                AddHeader(header.Key, header.Value);
            }
        }

        if(telemetry != null)
        {
            _telemetry = telemetry;
        }
    }

    /// <summary>
    ///     Internal constructor for deserialization scenarios.
    /// </summary>
    /// <param name="dto">The data transfer object (DTO) used for deserialization.</param>
    /// <param name="telemetry">Optional telemetry service for logging or diagnostics.</param>
    internal CompositeEnvelope(
        EnvelopeDto<IList<T>> dto,
        IResultTelemetry? telemetry = null)
        : base(dto, telemetry)
    {
        // No additional checks needed here, as base envelope constructor
        // already handles null-checking on dto.

        if(telemetry != null)
        {
            _telemetry = telemetry;
        }
    }

    /// <summary>
    ///     Adds a single payload to the composite envelope.
    /// </summary>
    /// <param name="payload">The payload to add. Must not be null if <typeparamref name="T" /> is a reference type.</param>
    /// <returns>
    ///     A <see cref="Result{TSuccess, TError}" /> indicating success or failure of the operation,
    ///     with an appropriate <see cref="ValidationError" /> on failure.
    /// </returns>
    /// <remarks>
    ///     This method creates a new list containing all existing payloads plus the new payload,
    ///     then attempts to update the underlying envelope state.
    ///     If you are frequently adding items in a loop, consider collecting them first and creating
    ///     a single updated list to reduce overhead.
    /// </remarks>
    public Result<Unit, ValidationError> AddPayload(T payload)
    {
        // Defensive check if T is a reference type (can also rely on calling code to pass non-null).
        if (payload == null && !typeof(T).IsValueType)
        {
            return Result<Unit, ValidationError>.Failure(
                new ValidationError("Cannot add a null payload to the envelope.")
            );
        }

        // Create a new list with the additional item
        var newPayloads = new List<T>(Payload) { payload };

        // Attempt to update the underlying payload in base envelope
        var result = TryModifyPayload(newPayloads);
        return result.IsSuccess
            ? Result<Unit, ValidationError>.Success(Unit.Value)
            : Result<Unit, ValidationError>.Failure(result.Error!);
    }

    /// <summary>
    ///     Removes a single payload from the composite envelope.
    /// </summary>
    /// <param name="payload">The payload to remove.</param>
    /// <returns>
    ///     A <see cref="Result{TSuccess, TError}" /> indicating success or failure of the operation,
    ///     with an appropriate <see cref="ValidationError" /> on failure.
    /// </returns>
    /// <remarks>
    ///     This method creates a new list with all items except the specified <paramref name="payload" />.
    ///     If multiple items match the payload, they all get removed.
    /// </remarks>
    public Result<Unit, ValidationError> RemovePayload(T payload)
    {
        // You might check for null if T is a reference type
        if (payload == null && !typeof(T).IsValueType)
        {
            return Result<Unit, ValidationError>.Failure(
                new ValidationError("Cannot remove a null payload from the envelope.")
            );
        }

        // Filter out all matches
        var newPayloads = Payload
            .Where(p => !EqualityComparer<T>.Default.Equals(p, payload))
            .ToList();

        // Attempt to update the underlying payload
        var result = TryModifyPayload(newPayloads);
        return result.IsSuccess
            ? Result<Unit, ValidationError>.Success(Unit.Value)
            : Result<Unit, ValidationError>.Failure(result.Error!);
    }

    /// <summary>
    ///     Filters payloads in the composite envelope based on a specified predicate,
    ///     returning a new <see cref="CompositeEnvelope{T}" /> containing only the matching items.
    /// </summary>
    /// <param name="predicate">The predicate to filter payloads by. Must not be null.</param>
    /// <returns>
    ///     A <see cref="Result{TSuccess, TError}" /> whose success value is the new
    ///     <see cref="CompositeEnvelope{T}" /> with filtered payloads, or a <see cref="ValidationError" /> on failure.
    /// </returns>
    public Result<CompositeEnvelope<T>, ValidationError> FilterPayloads(Func<T, bool> predicate)
    {
        if (predicate == null)
        {
            return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                new ValidationError("Predicate cannot be null.")
            );
        }

        try
        {
            var filteredPayloads = Payload.Where(predicate).ToList();
            var filteredEnvelope = new CompositeEnvelope<T>(filteredPayloads, null, _telemetry ?? new DefaultResultTelemetry());

            return Result<CompositeEnvelope<T>, ValidationError>.Success(filteredEnvelope);
        }
        catch (Exception ex)
        {
            // If the predicate throws, for example
            return Result<CompositeEnvelope<T>, ValidationError>.Failure(
                new ValidationError($"Payload filtering failed: {ex.Message}")
            );
        }
    }

    /// <summary>
    ///     Performs a batch operation on all payloads using the provided function.
    /// </summary>
    /// <param name="processingFunc">
    ///     A function that processes each payload, returning
    ///     a <see cref="Result{TSuccess, TError}" /> indicating success or failure for that item.
    /// </param>
    /// <returns>
    ///     A <see cref="Result{TSuccess, TError}" /> indicating success if all items were processed,
    ///     or a single <see cref="ValidationError" /> if any item fails.
    /// </returns>
    /// <remarks>
    ///     This method is currently atomic: if any item fails to process,
    ///     the entire operation is considered failed. The error contains how many items failed.
    /// </remarks>
    public Result<Unit, ValidationError> ProcessPayloads(Func<T, Result<Unit, ValidationError>> processingFunc)
    {
        if (processingFunc == null)
        {
            return Result<Unit, ValidationError>.Failure(
                new ValidationError("Processing function cannot be null.")
            );
        }


        var results = Payload.Select(item =>
        {
            try
            {
                return processingFunc(item);
            }
            catch (Exception ex)
            {
                // Wrap thrown exceptions in a failure result
                return Result<Unit, ValidationError>.Failure(new ValidationError($"Exception: {ex.Message}"));
            }
        }).ToList();

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
