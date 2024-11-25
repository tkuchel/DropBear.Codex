using DropBear.Codex.Blazor.Enums;

namespace DropBear.Codex.Blazor.Models;


public sealed class SnackbarInstance
{
    public string Id { get; } = $"snackbar-{Guid.NewGuid():N}";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public SnackbarType Type { get; set; } = SnackbarType.Information;
    public int Duration { get; set; } = 5000;
    public bool RequiresManualClose { get; set; }
    public List<SnackbarAction> Actions { get; set; } = new();
}
