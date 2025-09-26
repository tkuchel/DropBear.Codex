using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;

namespace DropBear.Codex.Blazor.Builders;

/// <summary>
///     Builder class for creating SnackbarInstance objects with fluent syntax.
/// </summary>
public sealed class SnackbarInstanceBuilder
{
    private string _id = Guid.NewGuid().ToString("N");
    private string? _title;
    private string _message = string.Empty;
    private SnackbarType _type = SnackbarType.Information;
    private int _duration = 5000;
    private int _showDelay = 0;
    private bool _requiresManualClose;
    private List<SnackbarAction>? _actions;
    private string? _cssClass;
    private Dictionary<string, object>? _metadata;

    /// <summary>
    ///     Sets the ID for the snackbar.
    /// </summary>
    public SnackbarInstanceBuilder WithId(string id)
    {
        _id = id ?? throw new ArgumentNullException(nameof(id));
        return this;
    }

    /// <summary>
    ///     Sets the title for the snackbar.
    /// </summary>
    public SnackbarInstanceBuilder WithTitle(string? title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    ///     Sets the message for the snackbar.
    /// </summary>
    public SnackbarInstanceBuilder WithMessage(string message)
    {
        _message = message ?? throw new ArgumentNullException(nameof(message));
        return this;
    }

    /// <summary>
    ///     Sets the type for the snackbar.
    /// </summary>
    public SnackbarInstanceBuilder WithType(SnackbarType type)
    {
        _type = type;
        return this;
    }

    /// <summary>
    ///     Sets the duration for the snackbar.
    /// </summary>
    public SnackbarInstanceBuilder WithDuration(int duration)
    {
        _duration = Math.Max(0, duration);
        return this;
    }

    /// <summary>
    ///     Sets the show delay for the snackbar.
    /// </summary>
    public SnackbarInstanceBuilder WithDelay(int delay)
    {
        _showDelay = Math.Max(0, Math.Min(10000, delay));
        return this;
    }

    /// <summary>
    ///     Sets whether the snackbar requires manual close.
    /// </summary>
    public SnackbarInstanceBuilder RequireManualClose(bool requireManualClose = true)
    {
        _requiresManualClose = requireManualClose;
        return this;
    }

    /// <summary>
    ///     Adds an action to the snackbar.
    /// </summary>
    public SnackbarInstanceBuilder WithAction(SnackbarAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _actions ??= new List<SnackbarAction>();
        _actions.Add(action);
        return this;
    }

    /// <summary>
    ///     Adds an action to the snackbar with simplified parameters.
    /// </summary>
    public SnackbarInstanceBuilder WithAction(string label, Func<Task> onClick, bool isPrimary = false)
    {
        var action = new SnackbarAction { Label = label, OnClick = onClick, IsPrimary = isPrimary };

        return WithAction(action);
    }

    /// <summary>
    ///     Adds multiple actions to the snackbar.
    /// </summary>
    public SnackbarInstanceBuilder WithActions(params SnackbarAction[] actions)
    {
        if (actions?.Length > 0)
        {
            _actions ??= new List<SnackbarAction>();
            _actions.AddRange(actions);
        }

        return this;
    }

    /// <summary>
    ///     Adds custom CSS classes.
    /// </summary>
    public SnackbarInstanceBuilder WithCssClass(string? cssClass)
    {
        _cssClass = cssClass;
        return this;
    }

    /// <summary>
    ///     Adds metadata to the snackbar.
    /// </summary>
    public SnackbarInstanceBuilder WithMetadata(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        _metadata ??= new Dictionary<string, object>();
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    ///     Adds multiple metadata entries.
    /// </summary>
    public SnackbarInstanceBuilder WithMetadata(IReadOnlyDictionary<string, object> metadata)
    {
        if (metadata?.Count > 0)
        {
            _metadata ??= new Dictionary<string, object>();
            foreach (var kvp in metadata)
            {
                _metadata[kvp.Key] = kvp.Value;
            }
        }

        return this;
    }

    /// <summary>
    ///     Builds the SnackbarInstance with the configured properties.
    /// </summary>
    public SnackbarInstance Build()
    {
        if (string.IsNullOrWhiteSpace(_message))
        {
            throw new InvalidOperationException("Message is required for snackbar instance.");
        }

        return new SnackbarInstance
        {
            Id = _id,
            Title = _title,
            Message = _message,
            Type = _type,
            Duration = _duration,
            ShowDelay = _showDelay,
            RequiresManualClose = _requiresManualClose,
            Actions = _actions?.AsReadOnly(),
            CssClass = _cssClass,
            Metadata = _metadata?.AsReadOnly(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
