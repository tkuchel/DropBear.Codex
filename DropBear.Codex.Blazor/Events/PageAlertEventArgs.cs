#region

using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Events;

/// <summary>
///     Represents the event arguments for page alert events.
/// </summary>
public sealed class PageAlertEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PageAlertEventArgs" /> class.
    /// </summary>
    /// <param name="alert">The page alert associated with the event.</param>
    public PageAlertEventArgs(PageAlert alert)
    {
        Alert = alert;
    }

    /// <summary>
    ///     Gets the page alert associated with the event.
    /// </summary>
    public PageAlert Alert { get; }
}
