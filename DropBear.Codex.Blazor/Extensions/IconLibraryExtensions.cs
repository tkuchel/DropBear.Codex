#region

using DropBear.Codex.Blazor.Helpers;
using DropBear.Codex.Blazor.Services;

#endregion

namespace DropBear.Codex.Blazor.Extensions;

public static class IconLibraryExtensions
{
    // builder.Services.AddSingleton<IconLibrary>();

    /// <summary>
    ///     Preloads common icons into the icon library.
    ///     This method is intended to be called during application startup.
    /// </summary>
    public static IconLibrary PreloadCommonIcons(this IconLibrary iconLibrary)
    {
        // Register common icons with descriptive keys
        iconLibrary.RegisterIcon("check", SvgIcons.Success);
        iconLibrary.RegisterIcon("error", SvgIcons.Error);
        iconLibrary.RegisterIcon("warning", SvgIcons.Warning);
        iconLibrary.RegisterIcon("info", SvgIcons.Information);
        iconLibrary.RegisterIcon("close",
            "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='1.5'><path d='M18 6L6 18M6 6l12 12'/></svg>");

        return iconLibrary; // For method chaining
    }
}
