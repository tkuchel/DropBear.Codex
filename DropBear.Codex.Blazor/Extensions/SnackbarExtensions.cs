using DropBear.Codex.Blazor.Builders;
using DropBear.Codex.Blazor.Interfaces;

namespace DropBear.Codex.Blazor.Extensions;

/// <summary>
///     Extension methods for snackbar-related operations.
/// </summary>
public static class SnackbarExtensions
{
    /// <summary>
    ///     Creates a snackbar builder for fluent configuration.
    /// </summary>
    /// <param name="service">The snackbar service.</param>
    /// <param name="message">The initial message.</param>
    /// <returns>A snackbar builder instance.</returns>
    public static SnackbarBuilder CreateSnackbar(this ISnackbarService service, string message)
    {
        return new SnackbarBuilder(service, message);
    }

    /// <summary>
    ///     Creates a new SnackbarInstanceBuilder.
    /// </summary>
    /// <param name="message">The initial message.</param>
    /// <returns>A new SnackbarInstanceBuilder instance.</returns>
    public static SnackbarInstanceBuilder CreateInstance(string message)
    {
        return new SnackbarInstanceBuilder().WithMessage(message);
    }

    /// <summary>
    ///     Creates a new SnackbarInstanceBuilder with title and message.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="message">The message.</param>
    /// <returns>A new SnackbarInstanceBuilder instance.</returns>
    public static SnackbarInstanceBuilder CreateInstance(string title, string message)
    {
        return new SnackbarInstanceBuilder()
            .WithTitle(title)
            .WithMessage(message);
    }
}

