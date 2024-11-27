namespace DropBear.Codex.Blazor.Models;

public abstract class SnackbarAction
{
    public string Label { get; set; } = string.Empty;
    public Func<Task>? OnClick { get; set; }
}
