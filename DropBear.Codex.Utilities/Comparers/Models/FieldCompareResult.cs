#region

using System.Text.Json.Serialization;

#endregion

namespace DropBear.Codex.Utilities.Comparers.Models;

/// <summary>
///     Represents the result of comparing a single field between two objects,
///     including the field's name, a confidence score, and any additional info.
/// </summary>
public sealed class FieldCompareResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FieldCompareResult" /> class.
    /// </summary>
    /// <param name="fieldName">The field/property name compared.</param>
    /// <param name="confidence">A score indicating how similar the two values are (range [0..1]).</param>
    /// <param name="additionalInfo">Optional extra information about the comparison.</param>
    public FieldCompareResult(string fieldName, double confidence, string? additionalInfo = null)
    {
        FieldName = fieldName;
        Confidence = Math.Max(0, Math.Min(1, confidence)); // Ensure confidence is in [0..1] range
        AdditionalInfo = additionalInfo;
    }

    /// <summary>
    ///     Gets or sets the field/property name.
    /// </summary>
    public string FieldName { get; set; }

    /// <summary>
    ///     Gets or sets the confidence score for this field, in range [0..1].
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    ///     Gets or sets any additional info about the comparison.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AdditionalInfo { get; set; }

    /// <summary>
    ///     Gets a value indicating whether this field is considered a match.
    /// </summary>
    [JsonIgnore]
    public bool IsMatch => Confidence > 0.8;

    /// <summary>
    ///     Gets a value indicating whether this field has a partial match.
    /// </summary>
    [JsonIgnore]
    public bool IsPartialMatch => Confidence is > 0.3 and <= 0.8;

    /// <summary>
    ///     Gets a value indicating whether this field is considered not a match.
    /// </summary>
    [JsonIgnore]
    public bool IsNotMatch => Confidence <= 0.3;

    /// <summary>
    ///     Gets a human-readable description of the confidence level.
    /// </summary>
    [JsonIgnore]
    public string ConfidenceDescription =>
        Confidence switch
        {
            >= 0.9 => "Excellent match",
            >= 0.8 => "Good match",
            >= 0.6 => "Moderate match",
            >= 0.4 => "Weak match",
            >= 0.2 => "Poor match",
            > 0.0 => "Very poor match",
            _ => "No match"
        };
}
