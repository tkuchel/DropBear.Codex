#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Configuration for a progress step with modern .NET features.
/// </summary>
public sealed record ProgressStepConfig
{
    /// <summary>
    ///     Gets the unique identifier for this step.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    ///     Gets the display name for this step.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Gets the optional description for this step.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Gets the optional tooltip text for this step.
    /// </summary>
    public string? Tooltip { get; init; }

    /// <summary>
    ///     Gets the optional icon identifier for this step.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    ///     Gets whether this step is required.
    /// </summary>
    public bool IsRequired { get; init; } = true;

    /// <summary>
    ///     Gets the estimated duration for this step.
    /// </summary>
    public TimeSpan? EstimatedDuration { get; init; }

    /// <summary>
    ///     Gets any custom properties for this step.
    /// </summary>
    public IReadOnlyDictionary<string, object>? CustomProperties { get; init; }
}
