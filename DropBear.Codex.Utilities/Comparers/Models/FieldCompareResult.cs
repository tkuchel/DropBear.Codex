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
    public FieldCompareResult(string fieldName, double confidence, string additionalInfo = "")
    {
        FieldName = fieldName;
        Confidence = confidence;
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
    public string AdditionalInfo { get; set; }
}
