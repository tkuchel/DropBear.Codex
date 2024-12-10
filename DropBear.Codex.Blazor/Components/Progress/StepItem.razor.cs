#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Progress;

public sealed partial class StepItem : DropBearComponentBase
{
    [CascadingParameter] public DropBearProgressBar ProgressBar { get; set; } = null!;
    [Parameter] public required ProgressStep Step { get; set; }
    [Parameter] public bool IsActive { get; set; }
    [Parameter] public double Progress { get; set; }
    [Parameter] public EventCallback<ProgressStep> OnHover { get; set; }
    [Parameter] public ProgressStep? HoveredStep { get; set; }
    [Parameter] public StepPosition Position { get; set; }

    private bool IsHovered
    {
        get => HoveredStep == Step;
        set => throw new NotImplementedException();
    }

    private string GetProgressStyle()
    {
        return $"width: {Progress}%; {(IsActive ? "animation: progressPulse 2s infinite;" : "")}";
    }

    private string GetStatusIcon()
    {
        return Step.Status switch
        {
            StepStatus.Completed => "icon-check-circle",
            StepStatus.Warning => "icon-alert-triangle",
            StepStatus.Error => "icon-x-circle",
            StepStatus.Active => "icon-spinner",
            _ => "icon-circle"
        };
    }

    private string GetStatusText()
    {
        return Step.Status switch
        {
            StepStatus.Completed => "Completed",
            StepStatus.Warning => "Warning",
            StepStatus.Error => "Error",
            StepStatus.Active => "In Progress",
            _ => "Pending"
        };
    }

    private string GetStepClass()
    {
        var classes = new List<string>();

        if (IsActive)
        {
            classes.Add("active");
        }

        if (Step.Status == StepStatus.Completed)
        {
            classes.Add("completed");
        }

        if (Step.Status == StepStatus.Warning)
        {
            classes.Add("warning");
        }

        if (Step.Status == StepStatus.Error)
        {
            classes.Add("error");
        }

        return string.Join(" ", classes);
    }

    private string GetStepIcon()
    {
        return Step.Type switch
        {
            StepType.Users => "icon-users",
            StepType.Files => "icon-file-text",
            StepType.Database => "icon-database",
            _ => "icon-circle"
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1 ? $"{duration.TotalHours:F1}h" : $"{duration.TotalMinutes:F0}m";
    }


    private async Task HandleMouseOver()
    {
        IsHovered = true;
        await OnHover.InvokeAsync(Step);
    }

    private void HandleMouseOut()
    {
        IsHovered = false;
    }
}
