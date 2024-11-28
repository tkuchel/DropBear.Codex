#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

public sealed class PageAlertInstance
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public PageAlertType Type { get; init; }
    public int? Duration { get; init; }
    public bool IsPermanent { get; init; }
}
