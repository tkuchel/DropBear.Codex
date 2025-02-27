#region

using System.Text.Json.Serialization;

#endregion

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
        AdditionalInfo = null;
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
        AdditionalInfo = null;
    }

    /// <summary>
    ///     Initializes a new instance of <see cref="CompareResult" /> with an overall confidence,
    ///     a list of field results, and additional information.
    /// </summary>
    /// <param name="overallConfidence">The overall confidence score (0 to 1).</param>
    /// <param name="fieldResults">Field-by-field comparison results.</param>
    /// <param name="additionalInfo">Additional context or information about the comparison.</param>
    public CompareResult(double overallConfidence, IList<FieldCompareResult>? fieldResults, string? additionalInfo)
    {
        OverallConfidence = overallConfidence;
        FieldResults = fieldResults ?? new List<FieldCompareResult>();
        AdditionalInfo = additionalInfo;
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
    ///     Gets or sets additional information about the comparison.
    /// </summary>
    /// <remarks>
    ///     This can include details about why certain fields matched or didn't match,
    ///     or context that might be helpful for interpreting the results.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AdditionalInfo { get; set; }

    /// <summary>
    ///     Gets a value indicating whether all fields had a confidence > 0,
    ///     suggesting a successful match in every field.
    /// </summary>
    [JsonIgnore]
    public bool IsSuccessful => FieldResults.All(fr => fr.Confidence > 0);

    /// <summary>
    ///     Gets a value indicating whether the comparison shows a high confidence match.
    /// </summary>
    /// <remarks>
    ///     A threshold of 0.8 (80%) is used to determine a high confidence match.
    /// </remarks>
    [JsonIgnore]
    public bool IsHighConfidence => OverallConfidence >= 0.8;

    /// <summary>
    ///     Gets the names of fields that had the highest confidence scores.
    /// </summary>
    /// <param name="count">The maximum number of field names to return.</param>
    /// <returns>An array of field names with the highest confidence scores.</returns>
    public string[] GetBestMatchingFields(int count = 3)
    {
        return FieldResults
            .OrderByDescending(f => f.Confidence)
            .Take(count)
            .Select(f => f.FieldName)
            .ToArray();
    }

    /// <summary>
    ///     Gets the names of fields that had the lowest confidence scores.
    /// </summary>
    /// <param name="count">The maximum number of field names to return.</param>
    /// <returns>An array of field names with the lowest confidence scores.</returns>
    public string[] GetWorstMatchingFields(int count = 3)
    {
        return FieldResults
            .OrderBy(f => f.Confidence)
            .Take(count)
            .Select(f => f.FieldName)
            .ToArray();
    }
}
