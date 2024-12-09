#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

public class ProgressStep
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public StepStatus Status { get; set; }
    public StepType Type { get; set; }
    public string? Detail { get; set; }
    public TimeSpan? Duration { get; set; }
}
