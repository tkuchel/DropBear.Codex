#region

using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Interface for a service that provides dynamic context menu items.
/// </summary>
public interface IDynamicContextMenuService
{
    /// <summary>
    ///     Gets the menu items asynchronously based on the provided context and menu type.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <param name="context">The context for which the menu items are requested.</param>
    /// <param name="menuType">The type of the menu.</param>
    /// <returns>A task representing the asynchronous operation, with a list of context menu items as the result.</returns>
    Task<IReadOnlyList<ContextMenuItem>> GetMenuItemsAsync<TContext>(TContext context, string menuType);
}
