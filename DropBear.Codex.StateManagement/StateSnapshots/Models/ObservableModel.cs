#region

using System.ComponentModel;
using R3;

#endregion

// For Subject<T> presumably
// Make sure you have the correct namespace for Subject<T> (e.g. from Reactive Extensions or a custom type)

namespace DropBear.Codex.StateManagement.StateSnapshots.Models;

/// <summary>
///     A model that notifies observers about state changes and supports property change notifications.
/// </summary>
/// <typeparam name="T">The type of the state managed by this model.</typeparam>
public class ObservableModel<T> : INotifyPropertyChanged
{
    private readonly object _lock = new();
    private T _state = default!;

    /// <summary>
    ///     Gets a subject that emits events whenever the state changes.
    /// </summary>
    public Subject<T> StateChanged { get; } = new();

    /// <summary>
    ///     Gets or sets the state. Notifies observers if the state changes.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the new state value is invalid.</exception>
    public T State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
        set
        {
            if (!IsValid(value))
            {
                throw new ArgumentException("Invalid state value.", nameof(value));
            }

            T oldValue;
            bool changed;

            lock (_lock)
            {
                if (Equals(_state, value))
                {
                    return; // no change
                }

                oldValue = _state;
                _state = value;
                changed = true;
            }

            if (changed)
            {
                StateChanged.OnNext(value);
                OnPropertyChanged(nameof(State));
            }
        }
    }

    /// <summary>
    ///     Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Validates the state value. By default, checks that <paramref name="value" /> is not <c>null</c>.
    ///     Override or modify if needed.
    /// </summary>
    private static bool IsValid(T value)
    {
        return value is not null;
    }

    /// <summary>
    ///     Raises the <see cref="PropertyChanged" /> event for the given property name.
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
