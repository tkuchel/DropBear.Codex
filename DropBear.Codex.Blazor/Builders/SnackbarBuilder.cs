using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;

namespace DropBear.Codex.Blazor.Builders;

/// <summary>
///     Fluent builder for creating snackbar instances and showing them via the service.
/// </summary>
public sealed class SnackbarBuilder
{
    private readonly ISnackbarService _service;
    private readonly SnackbarInstanceBuilder _builder;

    internal SnackbarBuilder(ISnackbarService service, string message)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _builder = new SnackbarInstanceBuilder().WithMessage(message);
    }

    /// <summary>
    ///     Sets the ID for the snackbar.
    /// </summary>
    public SnackbarBuilder WithId(string id)
    {
        _builder.WithId(id);
        return this;
    }

    /// <summary>
    ///     Sets the title for the snackbar.
    /// </summary>
    public SnackbarBuilder WithTitle(string? title)
    {
        _builder.WithTitle(title);
        return this;
    }

    /// <summary>
    ///     Sets the type for the snackbar.
    /// </summary>
    public SnackbarBuilder WithType(SnackbarType type)
    {
        _builder.WithType(type);
        return this;
    }

    /// <summary>
    ///     Sets the duration for the snackbar.
    /// </summary>
    public SnackbarBuilder WithDuration(int duration)
    {
        _builder.WithDuration(duration);
        return this;
    }

    /// <summary>
    ///     Sets the show delay for the snackbar.
    /// </summary>
    public SnackbarBuilder WithDelay(int delay)
    {
        _builder.WithDelay(delay);
        return this;
    }

    /// <summary>
    ///     Sets whether the snackbar requires manual close.
    /// </summary>
    public SnackbarBuilder RequireManualClose(bool requireManualClose = true)
    {
        _builder.RequireManualClose(requireManualClose);
        return this;
    }

    /// <summary>
    ///     Adds an action to the snackbar.
    /// </summary>
    public SnackbarBuilder WithAction(string label, Func<Task> onClick, bool isPrimary = false)
    {
        _builder.WithAction(label, onClick, isPrimary);
        return this;
    }

    /// <summary>
    ///     Adds an action to the snackbar.
    /// </summary>
    public SnackbarBuilder WithAction(SnackbarAction action)
    {
        _builder.WithAction(action);
        return this;
    }

    /// <summary>
    ///     Adds multiple actions to the snackbar.
    /// </summary>
    /// <remarks>
    ///     Uses params ReadOnlySpan for zero-allocation when called with inline arguments.
    /// </remarks>
    public SnackbarBuilder WithActions(params ReadOnlySpan<SnackbarAction> actions)
    {
        _builder.WithActions(actions);
        return this;
    }

    /// <summary>
    ///     Adds custom CSS classes.
    /// </summary>
    public SnackbarBuilder WithCssClass(string? cssClass)
    {
        _builder.WithCssClass(cssClass);
        return this;
    }

    /// <summary>
    ///     Adds metadata to the snackbar.
    /// </summary>
    public SnackbarBuilder WithMetadata(string key, object value)
    {
        _builder.WithMetadata(key, value);
        return this;
    }

    /// <summary>
    ///     Adds multiple metadata entries.
    /// </summary>
    public SnackbarBuilder WithMetadata(IReadOnlyDictionary<string, object> metadata)
    {
        _builder.WithMetadata(metadata);
        return this;
    }

    /// <summary>
    ///     Builds and shows the configured snackbar.
    /// </summary>
    public async Task ShowAsync(CancellationToken cancellationToken = default)
    {
        var snackbar = _builder.Build();
        await _service.Show(snackbar, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Builds the snackbar instance without showing it.
    /// </summary>
    public SnackbarInstance Build()
    {
        return _builder.Build();
    }
}
