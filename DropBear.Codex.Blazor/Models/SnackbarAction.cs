namespace DropBear.Codex.Blazor.Models;

public class SnackbarAction
{
    public string Label { get; set; } = string.Empty;
    public Func<Task>? OnClick { get; set; }
}
