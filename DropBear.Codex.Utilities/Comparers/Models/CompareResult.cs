namespace DropBear.Codex.Utilities.Comparers.Models;

/// <summary>
///     Represents the result of a comparison operation,
///     including an overall confidence score and field-by-field results.
/// </summary>
public sealed class CompareResult
{
    /// <summary>
    ///     Initializes a new instance of <see cref="CompareResult" /> with an overall confidence
    ///     and an empty list of field results.
    /// </summary>
    /// <param name="overallConfidence">The overall confidence score (0 to 1).</param>
    public CompareResult(double overallConfidence)
    {
        OverallConfidence = overallConfidence;
        FieldResults = new List<FieldCompareResult>();
    }

    /// <summary>
    ///     Initializes a new instance of <see cref="CompareResult" /> with an overall confidence
    ///     and the given list of field results.
    /// </summary>
    /// <param name="overallConfidence">The overall confidence score (0 to 1).</param>
    /// <param name="fieldResults">
    ///     A list of field-by-field comparison results (may be <c>null</c> or empty).
    /// </param>
    public CompareResult(double overallConfidence, IList<FieldCompareResult>? fieldResults)
    {
        OverallConfidence = overallConfidence;
        FieldResults = fieldResults ?? new List<FieldCompareResult>();
    }

    /// <summary>
    ///     Gets or sets the overall confidence of the comparison, in range [0..1].
    /// </summary>
    public double OverallConfidence { get; set; }

    /// <summary>
    ///     Gets or sets the collection of field-level results for this comparison.
    /// </summary>
    public IList<FieldCompareResult> FieldResults { get; set; }

    /// <summary>
    ///     Gets a value indicating whether all fields had a confidence > 0,
    ///     suggesting a successful match in every field.
    /// </summary>
    public bool IsSuccessful => FieldResults.All(fr => fr.Confidence > 0);
}
