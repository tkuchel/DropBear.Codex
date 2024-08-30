namespace DropBear.Codex.Utilities.Comparers.Models;

public sealed class CompareResult
{
    public CompareResult(double overallConfidence)
    {
        OverallConfidence = overallConfidence;
        FieldResults = new List<FieldCompareResult>();
    }

    public CompareResult(double overallConfidence, IList<FieldCompareResult>? fieldResults)
    {
        OverallConfidence = overallConfidence;
        FieldResults = fieldResults ?? new List<FieldCompareResult>();
    }

    public double OverallConfidence { get; set; }
    public IList<FieldCompareResult> FieldResults { get; set; }

    public bool IsSuccessful => FieldResults.All(fr => fr.Confidence > 0);
}
