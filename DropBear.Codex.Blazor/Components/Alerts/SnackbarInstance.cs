#region

using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

public sealed record SnackbarInstance : SnackbarNotificationOptions
{
    public SnackbarInstance(SnackbarNotificationOptions options)
        : base(options.Title, options.Message, options.Type, options.Duration,
            options.IsDismissible, options.ActionText, options.OnAction)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public DateTimeOffset CreatedAt { get; }
    public DropBearSnackbarNotification? ComponentRef { get; set; }
}
