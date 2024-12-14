#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

public class StepState
{
    public StepState(string stepId, string name, string tooltip)
    {
        StepId = stepId;
        Name = name;
        Tooltip = tooltip;
        Progress = 0;
        Status = StepStatus.NotStarted;
    }

    public string StepId { get; }
    public string Name { get; }
    public string Tooltip { get; }
    public double Progress { get; private set; }
    public StepStatus Status { get; private set; }

    public event Action<StepState>? OnStateChanged;

    public void UpdateProgress(double progress, StepStatus status)
    {
        Progress = progress;
        Status = status;
        OnStateChanged?.Invoke(this);
    }
}
