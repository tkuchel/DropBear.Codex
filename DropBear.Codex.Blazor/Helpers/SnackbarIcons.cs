﻿#region

using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Helpers;

/// <summary>
///     Helper class for providing SVG icons for snackbar types.
/// </summary>
public static class SnackbarIcons
{
    /// <summary>
    ///     Gets the SVG icon markup for the specified snackbar type.
    /// </summary>
    /// <param name="type">The snackbar type.</param>
    /// <returns>MarkupString containing the SVG icon.</returns>
    public static MarkupString GetIcon(SnackbarType type) => type switch
    {
        SnackbarType.Success => SuccessIcon,
        SnackbarType.Error => ErrorIcon,
        SnackbarType.Warning => WarningIcon,
        SnackbarType.Information => InformationIcon,
        _ => InformationIcon
    };

    /// <summary>
    ///     Success checkmark icon.
    /// </summary>
    public static readonly MarkupString SuccessIcon = new(
        "<svg viewBox=\"0 0 20 20\" fill=\"currentColor\">" +
        "<path fill-rule=\"evenodd\" d=\"M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z\" clip-rule=\"evenodd\"/>" +
        "</svg>"
    );

    /// <summary>
    ///     Error exclamation icon.
    /// </summary>
    public static readonly MarkupString ErrorIcon = new(
        "<svg viewBox=\"0 0 20 20\" fill=\"currentColor\">" +
        "<path fill-rule=\"evenodd\" d=\"M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-8-5a1 1 0 011 1v3a1 1 0 11-2 0V6a1 1 0 011-1zm0 8a1 1 0 011-1v.01a1 1 0 11-2 0V13a1 1 0 011-1z\" clip-rule=\"evenodd\"/>" +
        "</svg>"
    );

    /// <summary>
    ///     Warning triangle icon.
    /// </summary>
    public static readonly MarkupString WarningIcon = new(
        "<svg viewBox=\"0 0 20 20\" fill=\"currentColor\">" +
        "<path fill-rule=\"evenodd\" d=\"M8.485 2.495c.673-1.167 2.357-1.167 3.03 0l6.28 10.875c.673 1.167-.17 2.625-1.516 2.625H3.72c-1.347 0-2.189-1.458-1.515-2.625L8.485 2.495zM10 5a.75.75 0 01.75.75v3.5a.75.75 0 01-1.5 0v-3.5A.75.75 0 0110 5zm0 9a1 1 0 100-2 1 1 0 000 2z\" clip-rule=\"evenodd\"/>" +
        "</svg>"
    );

    /// <summary>
    ///     Information circle icon.
    /// </summary>
    public static readonly MarkupString InformationIcon = new(
        "<svg viewBox=\"0 0 20 20\" fill=\"currentColor\">" +
        "<path fill-rule=\"evenodd\" d=\"M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a.75.75 0 000 1.5h.253a.25.25 0 01.244.304l-.459 2.066A1.75 1.75 0 0010.747 15H11a.75.75 0 000-1.5h-.253a.25.25 0 01-.244-.304l.459-2.066A1.75 1.75 0 009.253 9H9z\" clip-rule=\"evenodd\"/>" +
        "</svg>"
    );

    /// <summary>
    ///     Close/dismiss icon.
    /// </summary>
    public static readonly MarkupString CloseIcon = new(
        "<svg viewBox=\"0 0 20 20\" fill=\"currentColor\">" +
        "<path d=\"M6.28 5.22a.75.75 0 00-1.06 1.06L8.94 10l-3.72 3.72a.75.75 0 101.06 1.06L10 11.06l3.72 3.72a.75.75 0 101.06-1.06L11.06 10l3.72-3.72a.75.75 0 00-1.06-1.06L10 8.94 6.28 5.22z\"/>" +
        "</svg>"
    );
}
