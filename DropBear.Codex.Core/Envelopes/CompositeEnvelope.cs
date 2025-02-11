namespace DropBear.Codex.Core.Envelopes;

#region Composite Envelope

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
    public CompositeEnvelope(IList<T> payloads, IDictionary<string, object>? headers = null)
        : base(payloads, headers)
    {
    }

    // Additional helper methods for composite processing can be added here.
}

#endregion
