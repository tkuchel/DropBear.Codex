namespace DropBear.Codex.Utilities.Comparers.Models;

public sealed class FieldCompareResult
{
    public FieldCompareResult(string fieldName, double confidence, string additionalInfo = "")
    {
        FieldName = fieldName;
        Confidence = confidence;
        AdditionalInfo = additionalInfo;
    }

    public string FieldName { get; set; }
    public double Confidence { get; set; }
    public string AdditionalInfo { get; set; }
}
